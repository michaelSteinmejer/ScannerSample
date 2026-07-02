namespace ScannerSample.Wpf.Models
{
    public sealed class ScannerPreflightResult
    {
        public bool CanScan { get; set; }
        public string Message { get; set; }
        public ScannerCapabilities Capabilities { get; set; }

        public static ScannerPreflightResult Success(ScannerCapabilities capabilities, string message)
        {
            return new ScannerPreflightResult
            {
                CanScan = true,
                Capabilities = capabilities,
                Message = message
            };
        }

        public static ScannerPreflightResult Blocked(ScannerCapabilities capabilities, string message)
        {
            return new ScannerPreflightResult
            {
                CanScan = false,
                Capabilities = capabilities,
                Message = message
            };
        }
    }
}
