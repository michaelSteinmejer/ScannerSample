using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using ScannerSample.Wpf.Providers;
using ScannerSample.Wpf.Services;
using ScannerSample.Wpf.ViewModels;

namespace ScannerSample.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var scanService = new ScanApplicationService(new IScannerProvider[]
            {
                new MockScannerProvider(),
                new WiaScannerProvider(),
                new TwainScannerProvider()
            });

            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScannerSampleOutput");
            DataContext = new MainWindowViewModel(scanService, outputFolder);
        }
    }

    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool && (bool)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool && (bool)value);
        }
    }
}
