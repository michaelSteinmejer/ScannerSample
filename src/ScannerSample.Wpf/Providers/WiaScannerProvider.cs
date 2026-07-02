using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Imaging;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public sealed class WiaScannerProvider : IScannerProvider
    {
        public string Name { get { return "WIA"; } }

        public IEnumerable<ScannerDevice> GetDevices()
        {
            Type managerType = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (managerType == null)
            {
                yield break;
            }

            dynamic manager = Activator.CreateInstance(managerType);
            foreach (dynamic info in manager.DeviceInfos)
            {
                if ((int)info.Type != 1)
                {
                    continue;
                }

                var id = (string)info.DeviceID;
                var name = ReadProperty(info.Properties, "Name") ?? id;
                yield return new ScannerDevice(id, name, Name);
            }
        }

        public IScannerSession Open(ScannerDevice device)
        {
            return new WiaScannerSession(device);
        }

        private static string ReadProperty(dynamic properties, string name)
        {
            foreach (dynamic property in properties)
            {
                if (string.Equals((string)property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(property.Value);
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
                    SupportsDuplex = false,
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

                    Type managerType = Type.GetTypeFromProgID("WIA.DeviceManager");
                    if (managerType == null)
                    {
                        throw new InvalidOperationException("WIA is not available on this machine.");
                    }

                    dynamic manager = Activator.CreateInstance(managerType);
                    dynamic deviceInfo = null;
                    foreach (dynamic info in manager.DeviceInfos)
                    {
                        if ((string)info.DeviceID == _device.Id)
                        {
                            deviceInfo = info;
                            break;
                        }
                    }

                    if (deviceInfo == null)
                    {
                        throw new InvalidOperationException("The selected WIA scanner was not found.");
                    }

                    dynamic device = deviceInfo.Connect();
                    dynamic commonDialog = Activator.CreateInstance(Type.GetTypeFromProgID("WIA.CommonDialog"));
                    dynamic item = device.Items[1];
                    Report(progress, ScanProgressKind.WaitingForScanner, 0, null, "Waiting for WIA scanner...");

                    TrySetWiaProperty(item.Properties, 6147, profile.Dpi);
                    TrySetWiaProperty(item.Properties, 6148, profile.Dpi);
                    TrySetWiaProperty(item.Properties, 6146, ToWiaColorMode(profile.ColorMode));

                    dynamic image = profile.ShowDriverUi
                        ? commonDialog.ShowTransfer(item)
                        : item.Transfer("{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}");

                    var file = Path.Combine(profile.OutputFolder, string.Format("wia-scan-{0:yyyyMMdd-HHmmss}.png", DateTime.Now));
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }

                    image.SaveFile(file);

                    var pages = new List<string> { file };
                    Report(progress, ScanProgressKind.PageScanned, pages.Count, file, "Page 1 scanned.");
                    Report(progress, ScanProgressKind.SavingDocument, pages.Count, null, "Saving document...");
                    var documentFile = profile.OutputFormat == ScanOutputFormat.MultiPageTiff
                        ? TiffDocumentWriter.SaveMultiPageTiff(pages, Path.ChangeExtension(file, ".tif"))
                        : null;

                    Report(progress, ScanProgressKind.Completed, pages.Count, documentFile, "Scan completed.");
                    return new ScanResult(pages, documentFile);
                }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
            }

            public void Dispose()
            {
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

            private static void TrySetWiaProperty(dynamic properties, int propertyId, object value)
            {
                try
                {
                    properties[propertyId].Value = value;
                }
                catch
                {
                    // Some WIA drivers reject standard properties. Keep the sample resilient.
                }
            }

            private static void Report(IProgress<ScanProgress> progress, ScanProgressKind kind, int pages, string file, string message)
            {
                if (progress != null)
                {
                    progress.Report(ScanProgress.Create(kind, pages, file, message));
                }
            }
        }
    }
}
