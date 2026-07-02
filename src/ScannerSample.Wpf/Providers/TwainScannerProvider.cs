using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NTwain;
using NTwain.Data;
using ScannerSample.Wpf.Imaging;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public sealed class TwainScannerProvider : IScannerProvider
    {
        public string Name { get { return "TWAIN / Ricoh PaperStream"; } }

        public IEnumerable<ScannerDevice> GetDevices()
        {
            using (var owner = TwainSessionOwner.Open())
            {
                foreach (var source in owner.Session)
                {
                    yield return new ScannerDevice(source.Name, source.Name, Name);
                }
            }
        }

        public IScannerSession Open(ScannerDevice device)
        {
            return new TwainScannerSession(device);
        }

        private sealed class TwainScannerSession : IScannerSession
        {
            private readonly ScannerDevice _device;
            private TwainSessionOwner _owner;
            private DataSource _source;

            public TwainScannerSession(ScannerDevice device)
            {
                _device = device;
            }

            public ScannerCapabilities GetCapabilities()
            {
                EnsureSourceOpen();

                return new ScannerCapabilities
                {
                    SupportsDuplex = CanReadCapability(_source.Capabilities.CapDuplexEnabled),
                    SupportsFeeder = CanReadCapability(_source.Capabilities.CapFeederEnabled),
                    CanCheckFeederLoaded = CanReadCapability(_source.Capabilities.CapFeederLoaded),
                    IsFeederLoaded = IsFeederLoaded(_source),
                    SupportsDoubleFeedDetection = CanReadCapability(_source.Capabilities.CapDoubleFeedDetection),
                    SupportsDriverUi = true
                };
            }

            public ScannerPreflightResult Preflight(ScanProfile profile)
            {
                var capabilities = GetCapabilities();
                if (profile.UseFeeder && capabilities.CanCheckFeederLoaded && !capabilities.IsFeederLoaded)
                {
                    return ScannerPreflightResult.Blocked(capabilities, "Laeg papir i feederen foer scanning.");
                }

                if (profile.DoubleFeedDetection && !capabilities.SupportsDoubleFeedDetection)
                {
                    return ScannerPreflightResult.Success(capabilities, "Feeder ready. Double-feed detection is not exposed by this TWAIN driver.");
                }

                return ScannerPreflightResult.Success(capabilities, profile.UseFeeder ? "Feeder ready." : "Scanner ready.");
            }

            public Task<ScanResult> ScanAsync(ScanProfile profile, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
            {
                EnsureSourceOpen();
                Directory.CreateDirectory(profile.OutputFolder);
                Report(progress, ScanProgressKind.Started, 0, null, "TWAIN scan started.");

                var pageFiles = new List<string>();
                var completion = new TaskCompletionSource<ScanResult>();

                EventHandler<TransferReadyEventArgs> transferReady = null;
                EventHandler<DataTransferredEventArgs> dataTransferred = null;
                EventHandler sourceDisabled = null;

                transferReady = (sender, args) =>
                {
                    Report(progress, ScanProgressKind.WaitingForScanner, pageFiles.Count, null, "TWAIN source is ready to transfer.");
                    if (cancellationToken.IsCancellationRequested)
                    {
                        args.CancelAll = true;
                    }
                };

                dataTransferred = (sender, args) =>
                {
                    try
                    {
                        if (args.NativeData == IntPtr.Zero)
                        {
                            return;
                        }

                        using (var stream = args.GetNativeImageStream())
                        {
                            if (stream == null)
                            {
                                return;
                            }

                            var file = Path.Combine(
                                profile.OutputFolder,
                                string.Format("twain-{0:yyyyMMdd-HHmmss}-p{1:00}.png", DateTime.Now, pageFiles.Count + 1));

                            using (var image = System.Drawing.Image.FromStream(stream))
                            {
                                image.Save(file, ImageFormat.Png);
                            }

                            pageFiles.Add(file);
                            Report(progress, ScanProgressKind.PageScanned, pageFiles.Count, file, string.Format("Page {0} scanned.", pageFiles.Count));
                        }
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                };

                sourceDisabled = (sender, args) =>
                {
                    try
                    {
                        Report(progress, ScanProgressKind.SavingDocument, pageFiles.Count, null, "Saving document...");
                        var documentFile = profile.OutputFormat == ScanOutputFormat.MultiPageTiff && pageFiles.Count > 0
                            ? TiffDocumentWriter.SaveMultiPageTiff(pageFiles, Path.Combine(profile.OutputFolder, string.Format("twain-{0:yyyyMMdd-HHmmss}.tif", DateTime.Now)))
                            : null;

                        Report(progress, ScanProgressKind.Completed, pageFiles.Count, documentFile, "Scan completed.");
                        completion.TrySetResult(new ScanResult(pageFiles, documentFile));
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                    finally
                    {
                        DetachHandlers(transferReady, dataTransferred, sourceDisabled);
                    }
                };

                cancellationToken.Register(() =>
                {
                    completion.TrySetCanceled();
                });

                _owner.Session.TransferReady += transferReady;
                _owner.Session.DataTransferred += dataTransferred;
                _owner.Session.SourceDisabled += sourceDisabled;

                try
                {
                    ApplyProfile(_source, profile);
                    var mode = profile.ShowDriverUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
                    _source.Enable(mode, false, IntPtr.Zero);
                }
                catch
                {
                    DetachHandlers(transferReady, dataTransferred, sourceDisabled);
                    throw;
                }

                return completion.Task;
            }

            public void Dispose()
            {
                if (_source != null)
                {
                    try
                    {
                        _source.Close();
                    }
                    catch
                    {
                    }

                    _source = null;
                }

                if (_owner != null)
                {
                    _owner.Dispose();
                    _owner = null;
                }
            }

            private void EnsureSourceOpen()
            {
                if (_source != null)
                {
                    return;
                }

                _owner = TwainSessionOwner.Open();
                _owner.Session.OpenSource(_device.Id);
                _source = _owner.Session.CurrentSource;
                if (_source == null)
                {
                    throw new InvalidOperationException("TWAIN source was not found: " + _device.Id);
                }
            }

            private void DetachHandlers(
                EventHandler<TransferReadyEventArgs> transferReady,
                EventHandler<DataTransferredEventArgs> dataTransferred,
                EventHandler sourceDisabled)
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Session.TransferReady -= transferReady;
                _owner.Session.DataTransferred -= dataTransferred;
                _owner.Session.SourceDisabled -= sourceDisabled;
            }

            private static void ApplyProfile(DataSource source, ScanProfile profile)
            {
                TrySet(() => source.Capabilities.CapXferCount.SetValue(profile.MaxPages > 0 ? profile.MaxPages : -1));
                TrySet(() => source.Capabilities.ICapXResolution.SetValue(profile.Dpi));
                TrySet(() => source.Capabilities.ICapYResolution.SetValue(profile.Dpi));
                TrySet(() => source.Capabilities.ICapPixelType.SetValue(ToTwainPixelType(profile.ColorMode)));
                TrySet(() => source.Capabilities.CapFeederEnabled.SetValue(profile.UseFeeder ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.CapAutoFeed.SetValue(profile.AutoFeed ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.CapDuplexEnabled.SetValue(profile.Duplex ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.CapIndicators.SetValue(profile.ShowIndicators ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.ICapAutoDiscardBlankPages.SetValue(profile.DiscardBlankPages ? BlankPage.Auto : BlankPage.Disable));
                TrySet(() => source.Capabilities.ICapAutomaticDeskew.SetValue(profile.AutoDeskew ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.ICapAutomaticRotate.SetValue(profile.AutoRotate ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.ICapAutomaticBorderDetection.SetValue(profile.AutoBorderDetection ? BoolType.True : BoolType.False));
                TrySet(() => source.Capabilities.CapDoubleFeedDetection.SetValue(profile.DoubleFeedDetection ? DoubleFeedDetection.Ultrasonic : DoubleFeedDetection.ByLength));
                TrySet(() => source.Capabilities.CapDoubleFeedDetectionSensitivity.SetValue(DoubleFeedDetectionSensitivity.Medium));
                TrySet(() => source.Capabilities.CapDoubleFeedDetectionResponse.SetValue(DoubleFeedDetectionResponse.Stop));
            }

            private static PixelType ToTwainPixelType(ScanColorMode colorMode)
            {
                switch (colorMode)
                {
                    case ScanColorMode.BlackAndWhite:
                        return PixelType.BlackWhite;
                    case ScanColorMode.Color:
                        return PixelType.RGB;
                    default:
                        return PixelType.Gray;
                }
            }

            private static bool CanReadCapability<T>(IReadOnlyCapWrapper<T> capability)
            {
                try
                {
                    return capability.CanGetCurrent;
                }
                catch
                {
                    return false;
                }
            }

            private static bool IsFeederLoaded(DataSource source)
            {
                try
                {
                    return source.Capabilities.CapFeederLoaded.GetCurrent() == BoolType.True;
                }
                catch
                {
                    return false;
                }
            }

            private static void TrySet(Action action)
            {
                try
                {
                    action();
                }
                catch
                {
                    // TWAIN capabilities vary by driver/profile. Unsupported settings should not stop scanning.
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

        private sealed class TwainSessionOwner : IDisposable
        {
            private TwainSessionOwner(TwainSession session)
            {
                Session = session;
            }

            public TwainSession Session { get; private set; }

            public static TwainSessionOwner Open()
            {
                var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
                var session = new TwainSession(appId);
                session.Open();
                return new TwainSessionOwner(session);
            }

            public void Dispose()
            {
                if (Session != null)
                {
                    try
                    {
                        Session.Close();
                    }
                    catch
                    {
                    }

                    Session = null;
                }
            }
        }
    }
}
