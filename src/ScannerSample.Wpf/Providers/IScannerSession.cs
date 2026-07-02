using System;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public interface IScannerSession : IDisposable
    {
        ScannerCapabilities GetCapabilities();
        ScannerPreflightResult Preflight(ScanProfile profile);
        Task<ScanResult> ScanAsync(ScanProfile profile, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
    }
}
