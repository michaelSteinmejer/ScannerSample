namespace ScannerSample.Wpf.Models
{
    public sealed class ScanProfile
    {
        public int Dpi { get; set; }
        public ScanColorMode ColorMode { get; set; }
        public bool Duplex { get; set; }
        public bool UseFeeder { get; set; }
        public bool ShowDriverUi { get; set; }
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
                OutputFolder = outputFolder,
                OutputFormat = ScanOutputFormat.PngPages
            };
        }
    }
}
