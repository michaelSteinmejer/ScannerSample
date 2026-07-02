namespace ScannerSample.Wpf.Models
{
    public enum ScanProgressKind
    {
        Started,
        WaitingForScanner,
        PageScanned,
        SavingDocument,
        Completed,
        Cancelled,
        Failed
    }
}
