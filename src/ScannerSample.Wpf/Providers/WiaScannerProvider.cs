using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Imaging;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public sealed class WiaScannerProvider : IScannerProvider
    {
        private const int ScannerDeviceType = 1;
        private const int HorizontalResolutionPropertyId = 6147;
        private const int VerticalResolutionPropertyId = 6148;
        private const int CurrentIntentPropertyId = 6146;
        private const int DocumentHandlingStatusPropertyId = 3087;
        private const int DocumentHandlingSelectPropertyId = 3088;
        private const int PagesPropertyId = 3096;
        private const int FeederReadyFlag = 1;
        private const int FeederSelectFlag = 1;
        private const int FlatbedSelectFlag = 2;
        private const int DuplexSelectFlag = 4;
        private const string PngFormatId = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

        public string Name { get { return "WIA"; } }

        public IEnumerable<ScannerDevice> GetDevices()
        {
            var manager = CreateComObject("WIA.DeviceManager");
            if (manager == null)
            {
                yield break;
            }

            foreach (var info in Enumerate(GetProperty(manager, "DeviceInfos")))
            {
                if (Convert.ToInt32(GetProperty(info, "Type")) != ScannerDeviceType)
                {
                    continue;
                }

                var id = Convert.ToString(GetProperty(info, "DeviceID"));
                var name = ReadProperty(GetProperty(info, "Properties"), "Name") ?? id;
                yield return new ScannerDevice(id, name, Name);
            }
        }

        public IScannerSession Open(ScannerDevice device)
        {
            return new WiaScannerSession(device);
        }

        private static string ReadProperty(object properties, string name)
        {
            foreach (var property in Enumerate(properties))
            {
                if (string.Equals(Convert.ToString(GetProperty(property, "Name")), name, StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(GetProperty(property, "Value"));
                }
            }

            return null;
        }

        private sealed class WiaScannerSession : IScannerSession
        {
            private readonly ScannerDevice _device;

            public WiaScannerSession(ScannerDevice device)
            {
                _device = device;
            }

            public ScannerCapabilities GetCapabilities()
            {
                return new ScannerCapabilities
                {
                    SupportsDuplex = true,
                    SupportsFeeder = true,
                    SupportsDriverUi = true
                };
            }

            public Task<ScanResult> ScanAsync(ScanProfile profile, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
            {
                return Task.Factory.StartNew(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(profile.OutputFolder);
                    Report(progress, ScanProgressKind.Started, 0, null, "WIA scan started.");

                    var deviceInfo = FindDeviceInfo(_device.Id);
                    if (deviceInfo == null)
                    {
                        throw new InvalidOperationException("The selected WIA scanner was not found.");
                    }

                    var device = Invoke(deviceInfo, "Connect");
                    var commonDialog = CreateComObject("WIA.CommonDialog");
                    var item = GetItem(GetProperty(device, "Items"), 1);
                    Report(progress, ScanProgressKind.WaitingForScanner, 0, null, "Waiting for WIA scanner...");

                    var deviceProperties = GetProperty(device, "Properties");
                    ConfigureDocumentHandling(deviceProperties, profile);

                    var itemProperties = GetProperty(item, "Properties");
                    TrySetWiaProperty(itemProperties, HorizontalResolutionPropertyId, profile.Dpi);
                    TrySetWiaProperty(itemProperties, VerticalResolutionPropertyId, profile.Dpi);
                    TrySetWiaProperty(itemProperties, CurrentIntentPropertyId, ToWiaColorMode(profile.ColorMode));
                    ApplyAdvancedItemProperties(itemProperties, profile);

                    var pages = ScanPages(profile, progress, cancellationToken, commonDialog, item, deviceProperties);
                    if (pages.Count == 0)
                    {
                        throw new InvalidOperationException("WIA scan completed without returning any pages.");
                    }

                    Report(progress, ScanProgressKind.SavingDocument, pages.Count, null, "Saving document...");
                    var documentFile = profile.OutputFormat == ScanOutputFormat.MultiPageTiff
                        ? TiffDocumentWriter.SaveMultiPageTiff(pages, Path.Combine(profile.OutputFolder, string.Format("wia-scan-{0:yyyyMMdd-HHmmss}.tif", DateTime.Now)))
                        : null;

                    Report(progress, ScanProgressKind.Completed, pages.Count, documentFile, "Scan completed.");
                    return new ScanResult(pages, documentFile);
                }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
            }

            public void Dispose()
            {
            }

            private static object FindDeviceInfo(string deviceId)
            {
                var manager = CreateComObject("WIA.DeviceManager");
                if (manager == null)
                {
                    throw new InvalidOperationException("WIA is not available on this machine.");
                }

                foreach (var info in Enumerate(GetProperty(manager, "DeviceInfos")))
                {
                    if (string.Equals(Convert.ToString(GetProperty(info, "DeviceID")), deviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return info;
                    }
                }

                return null;
            }

            private static void ConfigureDocumentHandling(object deviceProperties, ScanProfile profile)
            {
                var documentHandling = profile.UseFeeder ? FeederSelectFlag : FlatbedSelectFlag;
                if (profile.Duplex)
                {
                    documentHandling |= DuplexSelectFlag;
                }

                TrySetWiaProperty(deviceProperties, DocumentHandlingSelectPropertyId, documentHandling);

                if (profile.UseFeeder)
                {
                    // 0 asks WIA to scan all available pages when the driver honors WIA_DPS_PAGES.
                    TrySetWiaProperty(deviceProperties, PagesPropertyId, profile.MaxPages > 0 ? profile.MaxPages : 0);
                }
            }

            private static void ApplyAdvancedItemProperties(object itemProperties, ScanProfile profile)
            {
                TrySetNamedWiaProperty(itemProperties, profile.DiscardBlankPages, "blank");
                TrySetNamedWiaProperty(itemProperties, profile.AutoDeskew, "deskew");
                TrySetNamedWiaProperty(itemProperties, profile.AutoRotate, "rotate");
                TrySetNamedWiaProperty(itemProperties, profile.AutoBorderDetection, "border");
                TrySetNamedWiaProperty(itemProperties, profile.ShowIndicators, "indicator");
            }

            private static List<string> ScanPages(
                ScanProfile profile,
                IProgress<ScanProgress> progress,
                CancellationToken cancellationToken,
                object commonDialog,
                object item,
                object deviceProperties)
            {
                var pages = new List<string>();
                var shouldContinue = true;

                while (shouldContinue)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (profile.UseFeeder && pages.Count > 0 && !IsFeederReady(deviceProperties))
                    {
                        break;
                    }

                    if (profile.MaxPages > 0 && pages.Count >= profile.MaxPages)
                    {
                        break;
                    }

                    try
                    {
                        Report(progress, ScanProgressKind.WaitingForScanner, pages.Count, null, string.Format("Scanning WIA page {0}...", pages.Count + 1));
                        var image = profile.ShowDriverUi
                            ? Invoke(commonDialog, "ShowTransfer", item)
                            : Invoke(item, "Transfer", PngFormatId);

                        var file = Path.Combine(
                            profile.OutputFolder,
                            string.Format("wia-scan-{0:yyyyMMdd-HHmmss}-p{1:00}.png", DateTime.Now, pages.Count + 1));
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }

                        Invoke(image, "SaveFile", file);
                        pages.Add(file);
                        Report(progress, ScanProgressKind.PageScanned, pages.Count, file, string.Format("Page {0} scanned.", pages.Count));

                        shouldContinue = profile.UseFeeder && (profile.MaxPages == 0 || pages.Count < profile.MaxPages);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (pages.Count > 0 && IsExpectedEndOfFeeder(ex.InnerException))
                        {
                            break;
                        }

                        throw;
                    }
                    catch (COMException ex)
                    {
                        if (pages.Count > 0 && IsExpectedEndOfFeeder(ex))
                        {
                            break;
                        }

                        throw;
                    }
                }

                return pages;
            }

            private static bool IsFeederReady(object deviceProperties)
            {
                var status = TryGetIntWiaProperty(deviceProperties, DocumentHandlingStatusPropertyId);
                return !status.HasValue || (status.Value & FeederReadyFlag) == FeederReadyFlag;
            }

            private static bool IsExpectedEndOfFeeder(Exception exception)
            {
                var comException = exception as COMException;
                if (comException == null)
                {
                    return false;
                }

                // WIA drivers are not consistent here. Once at least one page has been returned,
                // a COM failure from the next transfer is commonly how "feeder empty" is reported.
                return comException.ErrorCode == unchecked((int)0x80210003) ||
                       comException.ErrorCode == unchecked((int)0x80210006) ||
                       comException.ErrorCode == unchecked((int)0x80210064);
            }

            private static int ToWiaColorMode(ScanColorMode colorMode)
            {
                switch (colorMode)
                {
                    case ScanColorMode.BlackAndWhite:
                        return 4;
                    case ScanColorMode.Color:
                        return 1;
                    default:
                        return 2;
                }
            }

            private static int? TryGetIntWiaProperty(object properties, int propertyId)
            {
                try
                {
                    return Convert.ToInt32(GetProperty(GetItem(properties, propertyId), "Value"));
                }
                catch
                {
                    return null;
                }
            }

            private static void TrySetWiaProperty(object properties, int propertyId, object value)
            {
                try
                {
                    SetProperty(GetItem(properties, propertyId), "Value", value);
                }
                catch
                {
                    // Some WIA drivers reject standard properties. Keep the sample resilient.
                }
            }

            private static void TrySetNamedWiaProperty(object properties, bool enabled, params string[] nameParts)
            {
                foreach (var property in Enumerate(properties))
                {
                    var name = Convert.ToString(GetProperty(property, "Name"));
                    if (!ContainsAll(name, nameParts))
                    {
                        continue;
                    }

                    if (TrySetWiaPropertyValue(property, enabled))
                    {
                        return;
                    }
                }
            }

            private static bool TrySetWiaPropertyValue(object property, bool enabled)
            {
                var values = enabled
                    ? new object[] { true, 1, -1, "True" }
                    : new object[] { false, 0, "False" };

                foreach (var value in values)
                {
                    try
                    {
                        SetProperty(property, "Value", value);
                        return true;
                    }
                    catch
                    {
                    }
                }

                return false;
            }

            private static bool ContainsAll(string text, params string[] parts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                foreach (var part in parts)
                {
                    if (text.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static void Report(IProgress<ScanProgress> progress, ScanProgressKind kind, int pages, string file, string message)
            {
                if (progress != null)
                {
                    progress.Report(ScanProgress.Create(kind, pages, file, message));
                }
            }
        }

        private static object CreateComObject(string progId)
        {
            var type = Type.GetTypeFromProgID(progId);
            return type == null ? null : Activator.CreateInstance(type);
        }

        private static IEnumerable<object> Enumerate(object collection)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null)
            {
                yield break;
            }

            foreach (var item in enumerable)
            {
                yield return item;
            }
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
        }

        private static void SetProperty(object target, string name, object value)
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }

        private static object GetItem(object collection, object index)
        {
            return collection.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, collection, new[] { index });
        }
    }
}
