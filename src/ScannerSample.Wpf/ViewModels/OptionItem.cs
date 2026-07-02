namespace ScannerSample.Wpf.ViewModels
{
    public sealed class OptionItem<T>
    {
        public OptionItem(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; private set; }
        public T Value { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
