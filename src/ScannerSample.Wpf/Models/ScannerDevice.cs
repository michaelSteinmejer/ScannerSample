namespace ScannerSample.Wpf.Models
{
    public sealed class ScannerDevice
    {
        public ScannerDevice(string id, string name, string providerName)
        {
            Id = id;
            Name = name;
            ProviderName = providerName;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string ProviderName { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, ProviderName);
        }
    }
}
