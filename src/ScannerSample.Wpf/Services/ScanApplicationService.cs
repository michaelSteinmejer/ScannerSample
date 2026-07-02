using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Models;
using ScannerSample.Wpf.Providers;

namespace ScannerSample.Wpf.Services
{
    public sealed class ScanApplicationService
    {
        private readonly List<IScannerProvider> _providers;

        public ScanApplicationService(IEnumerable<IScannerProvider> providers)
        {
            _providers = providers.ToList();
        }

        public IReadOnlyList<ScannerDevice> GetDevices()
        {
            var devices = new List<ScannerDevice>();
            foreach (var provider in _providers)
            {
                try
                {
                    devices.AddRange(provider.GetDevices());
                }
                catch
                {
                    // A failing provider should not prevent other scanner integrations from working.
                }
            }

            return devices;
        }

        public async Task<ScanResult> ScanAsync(
            ScannerDevice device,
            ScanProfile profile,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var provider = _providers.FirstOrDefault(x => string.Equals(x.Name, device.ProviderName, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                throw new InvalidOperationException("No scanner provider was found for " + device.ProviderName + ".");
            }

            using (var session = provider.Open(device))
            {
                return await session.ScanAsync(profile, progress, cancellationToken);
            }
        }
    }
}
