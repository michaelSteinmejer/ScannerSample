namespace ScannerSample.Wpf.Models
{
    public sealed class ScannerCapabilities
    {
        public bool SupportsDuplex { get; set; }
        public bool SupportsFeeder { get; set; }
        public bool IsFeederLoaded { get; set; }
        public bool CanCheckFeederLoaded { get; set; }
        public bool SupportsDoubleFeedDetection { get; set; }
        public bool SupportsDriverUi { get; set; }
    }
}
