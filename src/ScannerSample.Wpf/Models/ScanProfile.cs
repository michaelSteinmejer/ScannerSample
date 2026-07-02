namespace ScannerSample.Wpf.Models
{
    public sealed class ScanProfile
    {
        public int Dpi { get; set; }
        public ScanColorMode ColorMode { get; set; }
        public bool Duplex { get; set; }
        public bool UseFeeder { get; set; }
        public bool ShowDriverUi { get; set; }
        public bool AutoFeed { get; set; }
        public bool ShowIndicators { get; set; }
        public bool DiscardBlankPages { get; set; }
        public bool AutoDeskew { get; set; }
        public bool AutoRotate { get; set; }
        public bool AutoBorderDetection { get; set; }
        public int MaxPages { get; set; }
        public string OutputFolder { get; set; }
        public ScanOutputFormat OutputFormat { get; set; }

        public static ScanProfile Default(string outputFolder)
        {
            return new ScanProfile
            {
                Dpi = 300,
                ColorMode = ScanColorMode.Grayscale,
                Duplex = true,
                UseFeeder = true,
                ShowDriverUi = true,
                AutoFeed = true,
                ShowIndicators = true,
                DiscardBlankPages = false,
                AutoDeskew = true,
                AutoRotate = true,
                AutoBorderDetection = true,
                MaxPages = 0,
                OutputFolder = outputFolder,
                OutputFormat = ScanOutputFormat.PngPages
            };
        }
    }
}
