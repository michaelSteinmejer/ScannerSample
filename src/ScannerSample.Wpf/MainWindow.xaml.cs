using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ScannerSample.Wpf.Models;
using ScannerSample.Wpf.Providers;
using ScannerSample.Wpf.Services;

namespace ScannerSample.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly ScanApplicationService _scanService;
        private readonly ObservableCollection<string> _pageFiles;
        private CancellationTokenSource _scanCancellation;
        private string _outputFolder;

        public MainWindow()
        {
            InitializeComponent();

            _scanService = new ScanApplicationService(new IScannerProvider[]
            {
                new MockScannerProvider(),
                new WiaScannerProvider(),
                new TwainScannerProvider()
            });

            _pageFiles = new ObservableCollection<string>();
            PagesItemsControl.ItemsSource = _pageFiles;

            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScannerSampleOutput");
            OutputFolderTextBlock.Text = _outputFolder;

            RefreshDevices();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var device = DeviceComboBox.SelectedItem as ScannerDevice;
            if (device == null)
            {
                StatusTextBlock.Text = "Select a scanner first.";
                return;
            }

            SetScanningState(true);
            _pageFiles.Clear();
            _scanCancellation = new CancellationTokenSource();

            try
            {
                var profile = BuildProfile();
                StatusTextBlock.Text = string.Format("Scanning with {0}...", device.Name);

                var progress = new Progress<ScanProgress>(p =>
                {
                    if (!string.IsNullOrWhiteSpace(p.Message))
                    {
                        StatusTextBlock.Text = p.Message;
                    }

                    if (p.Kind == ScanProgressKind.PageScanned && !string.IsNullOrWhiteSpace(p.CurrentPageFile) && !_pageFiles.Contains(p.CurrentPageFile))
                    {
                        _pageFiles.Add(p.CurrentPageFile);
                    }
                });

                var result = await _scanService.ScanAsync(device, profile, progress, _scanCancellation.Token);

                StatusTextBlock.Text = result.DocumentFile == null
                    ? string.Format(CultureInfo.InvariantCulture, "Done. {0} page file(s) saved.", result.PageFiles.Count)
                    : string.Format(CultureInfo.InvariantCulture, "Done. {0} page file(s) and document saved: {1}", result.PageFiles.Count, result.DocumentFile);
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Scan failed: " + ex.Message;
            }
            finally
            {
                SetScanningState(false);
                if (_scanCancellation != null)
                {
                    _scanCancellation.Dispose();
                    _scanCancellation = null;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_scanCancellation != null)
            {
                _scanCancellation.Cancel();
            }
        }

        private void RefreshDevices()
        {
            var devices = _scanService.GetDevices();
            DeviceComboBox.ItemsSource = devices;
            DeviceComboBox.SelectedItem = devices.FirstOrDefault();
            StatusTextBlock.Text = devices.Count == 0
                ? "No scanners found. The mock provider should normally be available."
                : string.Format(CultureInfo.InvariantCulture, "{0} scanner device(s) found.", devices.Count);
        }

        private ScanProfile BuildProfile()
        {
            var dpiItem = (ComboBoxItem)DpiComboBox.SelectedItem;
            var colorItem = (ComboBoxItem)ColorModeComboBox.SelectedItem;
            var outputItem = (ComboBoxItem)OutputFormatComboBox.SelectedItem;

            var profile = ScanProfile.Default(_outputFolder);
            profile.Dpi = int.Parse((string)dpiItem.Content, CultureInfo.InvariantCulture);
            profile.ColorMode = (ScanColorMode)Enum.Parse(typeof(ScanColorMode), (string)colorItem.Content);
            profile.OutputFormat = (ScanOutputFormat)Enum.Parse(typeof(ScanOutputFormat), (string)outputItem.Content);
            profile.Duplex = DuplexCheckBox.IsChecked == true;
            profile.UseFeeder = FeederCheckBox.IsChecked == true;
            profile.ShowDriverUi = DriverUiCheckBox.IsChecked == true;
            return profile;
        }

        private void SetScanningState(bool isScanning)
        {
            ScanButton.IsEnabled = !isScanning;
            CancelButton.IsEnabled = isScanning;
            RefreshButton.IsEnabled = !isScanning;
            DeviceComboBox.IsEnabled = !isScanning;
        }
    }
}
