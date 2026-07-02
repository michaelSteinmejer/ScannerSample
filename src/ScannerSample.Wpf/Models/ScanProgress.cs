namespace ScannerSample.Wpf.Models
{
    public sealed class ScanProgress
    {
        public ScanProgressKind Kind { get; set; }
        public int PagesScanned { get; set; }
        public string CurrentPageFile { get; set; }
        public string Message { get; set; }

        public static ScanProgress Create(ScanProgressKind kind, int pagesScanned, string currentPageFile, string message)
        {
            return new ScanProgress
            {
                Kind = kind,
                PagesScanned = pagesScanned,
                CurrentPageFile = currentPageFile,
                Message = message
            };
        }
    }
}
