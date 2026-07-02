using System.Collections.Generic;

namespace ScannerSample.Wpf.Models
{
    public sealed class ScanResult
    {
        public ScanResult(IReadOnlyList<string> pageFiles, string documentFile)
        {
            PageFiles = pageFiles;
            DocumentFile = documentFile;
        }

        public IReadOnlyList<string> PageFiles { get; private set; }
        public string DocumentFile { get; private set; }
    }
}
