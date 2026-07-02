using System.Collections.Generic;
using ScannerSample.Wpf.Models;

namespace ScannerSample.Wpf.Providers
{
    public interface IScannerProvider
    {
        string Name { get; }
        IEnumerable<ScannerDevice> GetDevices();
        IScannerSession Open(ScannerDevice device);
    }
}
