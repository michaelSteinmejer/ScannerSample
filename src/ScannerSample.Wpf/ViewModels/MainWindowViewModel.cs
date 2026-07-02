using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScannerSample.Wpf.Models;
using ScannerSample.Wpf.Services;

namespace ScannerSample.Wpf.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly ScanApplicationService _scanService;
        private CancellationTokenSource _scanCancellation;
        private ScannerDevice _selectedDevice;
        private OptionItem<int> _selectedDpi;
        private OptionItem<ScanColorMode> _selectedColorMode;
        private OptionItem<int> _selectedMaxPages;
        private OptionItem<ScanOutputFormat> _selectedOutputFormat;
        private bool _isScanning;
        private bool _duplex = true;
        private bool _useFeeder = true;
        private bool _autoFeed = true;
        private bool _showDriverUi = true;
        private bool _showIndicators = true;
        private bool _discardBlankPages;
        private bool _autoDeskew = true;
        private bool _autoRotate = true;
        private bool _autoBorderDetection = true;
        private bool _doubleFeedDetection = true;
        private string _statusText;
        private string _feederStatusText;
        private string _doubleFeedStatusText;
        private PreflightState _preflightState;

        public MainWindowViewModel(ScanApplicationService scanService, string outputFolder)
        {
            _scanService = scanService;
            OutputFolder = outputFolder;

            Devices = new ObservableCollection<ScannerDevice>();
            PageFiles = new ObservableCollection<string>();
            DpiOptions = new ObservableCollection<OptionItem<int>>
            {
                new OptionItem<int>("200", 200),
                new OptionItem<int>("300", 300),
                new OptionItem<int>("400", 400),
                new OptionItem<int>("600", 600)
            };
            ColorModeOptions = new ObservableCollection<OptionItem<ScanColorMode>>
            {
                new OptionItem<ScanColorMode>("BlackAndWhite", ScanColorMode.BlackAndWhite),
                new OptionItem<ScanColorMode>("Grayscale", ScanColorMode.Grayscale),
                new OptionItem<ScanColorMode>("Color", ScanColorMode.Color)
            };
            MaxPagesOptions = new ObservableCollection<OptionItem<int>>
            {
                new OptionItem<int>("All", 0),
                new OptionItem<int>("1", 1),
                new OptionItem<int>("5", 5),
                new OptionItem<int>("10", 10),
                new OptionItem<int>("25", 25),
                new OptionItem<int>("50", 50)
            };
            OutputFormatOptions = new ObservableCollection<OptionItem<ScanOutputFormat>>
            {
                new OptionItem<ScanOutputFormat>("PngPages", ScanOutputFormat.PngPages),
                new OptionItem<ScanOutputFormat>("MultiPageTiff", ScanOutputFormat.MultiPageTiff)
            };

            _selectedDpi = DpiOptions[1];
            _selectedColorMode = ColorModeOptions[1];
            _selectedMaxPages = MaxPagesOptions[0];
            _selectedOutputFormat = OutputFormatOptions[0];
            _statusText = "Ready";
            _feederStatusText = "Feeder status not checked.";
            _doubleFeedStatusText = "Double-feed status not checked.";
            _preflightState = PreflightState.Unknown;

            RefreshDevicesCommand = new RelayCommand(RefreshDevices, () => !IsScanning);
            CheckFeederCommand = new RelayCommand(CheckFeeder, () => !IsScanning && SelectedDevice != null);
            StartScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning && SelectedDevice != null);
            CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);

            RefreshDevices();
        }

        public ObservableCollection<ScannerDevice> Devices { get; private set; }
        public ObservableCollection<string> PageFiles { get; private set; }
        public ObservableCollection<OptionItem<int>> DpiOptions { get; private set; }
        public ObservableCollection<OptionItem<ScanColorMode>> ColorModeOptions { get; private set; }
        public ObservableCollection<OptionItem<int>> MaxPagesOptions { get; private set; }
        public ObservableCollection<OptionItem<ScanOutputFormat>> OutputFormatOptions { get; private set; }

        public RelayCommand RefreshDevicesCommand { get; private set; }
        public RelayCommand CheckFeederCommand { get; private set; }
        public RelayCommand StartScanCommand { get; private set; }
        public RelayCommand CancelScanCommand { get; private set; }

        public string OutputFolder { get; private set; }

        public ScannerDevice SelectedDevice
        {
            get { return _selectedDevice; }
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    StartScanCommand.RaiseCanExecuteChanged();
                    CheckFeederCommand.RaiseCanExecuteChanged();
                    CheckFeeder();
                }
            }
        }

        public OptionItem<int> SelectedDpi
        {
            get { return _selectedDpi; }
            set { SetProperty(ref _selectedDpi, value); }
        }

        public OptionItem<ScanColorMode> SelectedColorMode
        {
            get { return _selectedColorMode; }
            set { SetProperty(ref _selectedColorMode, value); }
        }

        public OptionItem<int> SelectedMaxPages
        {
            get { return _selectedMaxPages; }
            set { SetProperty(ref _selectedMaxPages, value); }
        }

        public OptionItem<ScanOutputFormat> SelectedOutputFormat
        {
            get { return _selectedOutputFormat; }
            set { SetProperty(ref _selectedOutputFormat, value); }
        }

        public bool IsScanning
        {
            get { return _isScanning; }
            private set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    RefreshDevicesCommand.RaiseCanExecuteChanged();
                    CheckFeederCommand.RaiseCanExecuteChanged();
                    StartScanCommand.RaiseCanExecuteChanged();
                    CancelScanCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool Duplex
        {
            get { return _duplex; }
            set { SetProperty(ref _duplex, value); }
        }

        public bool UseFeeder
        {
            get { return _useFeeder; }
            set { SetProperty(ref _useFeeder, value); }
        }

        public bool AutoFeed
        {
            get { return _autoFeed; }
            set { SetProperty(ref _autoFeed, value); }
        }

        public bool ShowDriverUi
        {
            get { return _showDriverUi; }
            set { SetProperty(ref _showDriverUi, value); }
        }

        public bool ShowIndicators
        {
            get { return _showIndicators; }
            set { SetProperty(ref _showIndicators, value); }
        }

        public bool DiscardBlankPages
        {
            get { return _discardBlankPages; }
            set { SetProperty(ref _discardBlankPages, value); }
        }

        public bool AutoDeskew
        {
            get { return _autoDeskew; }
            set { SetProperty(ref _autoDeskew, value); }
        }

        public bool AutoRotate
        {
            get { return _autoRotate; }
            set { SetProperty(ref _autoRotate, value); }
        }

        public bool AutoBorderDetection
        {
            get { return _autoBorderDetection; }
            set { SetProperty(ref _autoBorderDetection, value); }
        }

        public bool DoubleFeedDetection
        {
            get { return _doubleFeedDetection; }
            set { SetProperty(ref _doubleFeedDetection, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public string FeederStatusText
        {
            get { return _feederStatusText; }
            private set { SetProperty(ref _feederStatusText, value); }
        }

        public string DoubleFeedStatusText
        {
            get { return _doubleFeedStatusText; }
            private set { SetProperty(ref _doubleFeedStatusText, value); }
        }

        public PreflightState PreflightState
        {
            get { return _preflightState; }
            private set { SetProperty(ref _preflightState, value); }
        }

        public void RefreshDevices()
        {
            var devices = _scanService.GetDevices();
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = Devices.FirstOrDefault();
            StatusText = Devices.Count == 0
                ? "No scanners found. The mock provider should normally be available."
                : string.Format(CultureInfo.InvariantCulture, "{0} scanner device(s) found.", Devices.Count);
        }

        public void CheckFeeder()
        {
            if (SelectedDevice == null)
            {
                PreflightState = PreflightState.Unknown;
                FeederStatusText = "No scanner selected.";
                DoubleFeedStatusText = "Double-feed status not checked.";
                return;
            }

            try
            {
                var result = _scanService.Preflight(SelectedDevice, BuildProfile());
                ApplyPreflightResult(result);
            }
            catch (Exception ex)
            {
                PreflightState = PreflightState.Warning;
                FeederStatusText = "Could not check feeder: " + ex.Message;
                DoubleFeedStatusText = "Double-feed status could not be checked.";
            }
        }

        private async Task ScanAsync()
        {
            if (SelectedDevice == null)
            {
                StatusText = "Select a scanner first.";
                return;
            }

            IsScanning = true;
            PageFiles.Clear();
            _scanCancellation = new CancellationTokenSource();

            try
            {
                var profile = BuildProfile();
                var preflight = _scanService.Preflight(SelectedDevice, profile);
                ApplyPreflightResult(preflight);
                StatusText = preflight.Message;
                if (!preflight.CanScan)
                {
                    return;
                }

                StatusText = string.Format("Scanning with {0}...", SelectedDevice.Name);

                var progress = new Progress<ScanProgress>(OnScanProgress);
                var result = await _scanService.ScanAsync(SelectedDevice, profile, progress, _scanCancellation.Token);

                StatusText = result.DocumentFile == null
                    ? string.Format(CultureInfo.InvariantCulture, "Done. {0} page file(s) saved.", result.PageFiles.Count)
                    : string.Format(CultureInfo.InvariantCulture, "Done. {0} page file(s) and document saved: {1}", result.PageFiles.Count, result.DocumentFile);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = "Scan failed: " + ex.Message;
            }
            finally
            {
                IsScanning = false;
                if (_scanCancellation != null)
                {
                    _scanCancellation.Dispose();
                    _scanCancellation = null;
                }
            }
        }

        private void CancelScan()
        {
            if (_scanCancellation != null)
            {
                _scanCancellation.Cancel();
            }
        }

        private void ApplyPreflightResult(ScannerPreflightResult result)
        {
            if (result == null)
            {
                PreflightState = PreflightState.Unknown;
                FeederStatusText = "Feeder status not checked.";
                DoubleFeedStatusText = "Double-feed status not checked.";
                return;
            }

            var capabilities = result.Capabilities;
            if (capabilities == null)
            {
                PreflightState = result.CanScan ? PreflightState.Warning : PreflightState.Blocked;
                FeederStatusText = result.Message;
                DoubleFeedStatusText = "Double-feed status unknown.";
                return;
            }

            if (!UseFeeder)
            {
                PreflightState = PreflightState.Ready;
                FeederStatusText = "Flatbed mode selected.";
            }
            else if (!capabilities.CanCheckFeederLoaded)
            {
                PreflightState = result.CanScan ? PreflightState.Warning : PreflightState.Blocked;
                FeederStatusText = "Feeder status is not exposed by this driver.";
            }
            else if (capabilities.IsFeederLoaded)
            {
                PreflightState = PreflightState.Ready;
                FeederStatusText = "Feeder ready: paper detected.";
            }
            else
            {
                PreflightState = PreflightState.Blocked;
                FeederStatusText = "Feeder empty: add paper before scanning.";
            }

            DoubleFeedStatusText = capabilities.SupportsDoubleFeedDetection
                ? "Double-feed detection available."
                : "Double-feed detection not exposed by this driver.";
        }

        private void OnScanProgress(ScanProgress progress)
        {
            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                StatusText = progress.Message;
            }

            if (progress.Kind == ScanProgressKind.PageScanned &&
                !string.IsNullOrWhiteSpace(progress.CurrentPageFile) &&
                !PageFiles.Contains(progress.CurrentPageFile))
            {
                PageFiles.Add(progress.CurrentPageFile);
            }
        }

        private ScanProfile BuildProfile()
        {
            var profile = ScanProfile.Default(OutputFolder);
            profile.Dpi = SelectedDpi.Value;
            profile.ColorMode = SelectedColorMode.Value;
            profile.OutputFormat = SelectedOutputFormat.Value;
            profile.Duplex = Duplex;
            profile.UseFeeder = UseFeeder;
            profile.AutoFeed = AutoFeed;
            profile.ShowDriverUi = ShowDriverUi;
            profile.ShowIndicators = ShowIndicators;
            profile.DiscardBlankPages = DiscardBlankPages;
            profile.AutoDeskew = AutoDeskew;
            profile.AutoRotate = AutoRotate;
            profile.AutoBorderDetection = AutoBorderDetection;
            profile.DoubleFeedDetection = DoubleFeedDetection;
            profile.MaxPages = SelectedMaxPages.Value;
            return profile;
        }
    }
}
