using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Imaging;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public sealed class MockScannerProvider : IScannerProvider
    {
        public string Name { get { return "Mock"; } }

        public IEnumerable<ScannerDevice> GetDevices()
        {
            yield return new ScannerDevice("mock:document-feeder", "Demo document feeder", Name);
        }

        public IScannerSession Open(ScannerDevice device)
        {
            return new MockScannerSession(device);
        }

        private sealed class MockScannerSession : IScannerSession
        {
            private readonly ScannerDevice _device;

            public MockScannerSession(ScannerDevice device)
            {
                _device = device;
            }

            public ScannerCapabilities GetCapabilities()
            {
                return new ScannerCapabilities
                {
                    SupportsDuplex = true,
                    SupportsFeeder = true,
                    SupportsDriverUi = false
                };
            }

            public async Task<ScanResult> ScanAsync(ScanProfile profile, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
            {
                Directory.CreateDirectory(profile.OutputFolder);
                Report(progress, ScanProgressKind.Started, 0, null, "Mock scan started.");

                var files = new List<string>();
                var pageCount = profile.Duplex ? 4 : 2;
                for (var page = 1; page <= pageCount; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Report(progress, ScanProgressKind.WaitingForScanner, files.Count, null, string.Format("Scanning page {0}...", page));
                    await Task.Delay(350, cancellationToken);

                    var file = Path.Combine(profile.OutputFolder, string.Format("mock-scan-{0:yyyyMMdd-HHmmss}-p{1:00}.png", DateTime.Now, page));
                    CreatePageImage(file, page, profile);
                    files.Add(file);
                    Report(progress, ScanProgressKind.PageScanned, files.Count, file, string.Format("Page {0} scanned.", files.Count));
                }

                Report(progress, ScanProgressKind.SavingDocument, files.Count, null, "Saving document...");
                var documentFile = profile.OutputFormat == ScanOutputFormat.MultiPageTiff
                    ? TiffDocumentWriter.SaveMultiPageTiff(files, Path.Combine(profile.OutputFolder, string.Format("mock-scan-{0:yyyyMMdd-HHmmss}.tif", DateTime.Now)))
                    : null;

                Report(progress, ScanProgressKind.Completed, files.Count, documentFile, "Scan completed.");
                return new ScanResult(files, documentFile);
            }

            public void Dispose()
            {
            }

            private void CreatePageImage(string file, int pageNumber, ScanProfile profile)
            {
                using (var bitmap = new Bitmap(1240, 1754))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var titleFont = new Font("Segoe UI", 42, FontStyle.Bold))
                using (var bodyFont = new Font("Segoe UI", 22))
                using (var pen = new Pen(Color.FromArgb(90, 90, 90), 4))
                {
                    graphics.Clear(Color.White);
                    graphics.DrawRectangle(pen, 60, 60, bitmap.Width - 120, bitmap.Height - 120);
                    graphics.DrawString("Scanner sample", titleFont, Brushes.Black, 110, 130);
                    graphics.DrawString(string.Format("Device: {0}", _device.Name), bodyFont, Brushes.DimGray, 110, 240);
                    graphics.DrawString(string.Format("Page: {0}", pageNumber), bodyFont, Brushes.DimGray, 110, 290);
                    graphics.DrawString(string.Format("Profile: {0} dpi, {1}, duplex={2}", profile.Dpi, profile.ColorMode, profile.Duplex), bodyFont, Brushes.DimGray, 110, 340);

                    for (var i = 0; i < 20; i++)
                    {
                        graphics.FillRectangle(Brushes.Gainsboro, 110, 460 + i * 52, 850 + (i % 4) * 45, 20);
                    }

                    bitmap.Save(file, ImageFormat.Png);
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
