using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class DashboardViewModel : PageViewModel
{
    private ScanResult? _scan;
    private bool _isScanning;
    private string _scanText = string.Empty;
    private string _cpuUsage = "–";
    private string _gpuUsage = "–";
    private string _ramUsage = "–";
    private string _thermalText = "Unavailable";
    private bool _subscribed;

    public DashboardViewModel()
    {
        RescanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        OptimizeCommand = new RelayCommand(() => MainViewModel.Instance?.Optimizer.StartOneClick());
        FortniteCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("games"));
        BoostLabCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("boostlab"));
        ThermalCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("thermal"));
        ScanService.Instance.Completed += r => Application.Current?.Dispatcher.BeginInvoke(() => Apply(r));
    }

    public override string Key => "dashboard";
    public override string Title => "Dashboard";
    public override string Icon => "";

    public ICommand RescanCommand { get; }
    public ICommand OptimizeCommand { get; }
    public ICommand FortniteCommand { get; }
    public ICommand BoostLabCommand { get; }
    public ICommand ThermalCommand { get; }

    public GamingModeViewModel GamingMode => GamingModeViewModel.Shared;

    public ObservableCollection<ReadinessCheck> Checks { get; } = new();

    public bool HasScan => _scan != null;
    public bool IsScanning { get => _isScanning; private set => SetProperty(ref _isScanning, value); }
    public string ScanText { get => _scanText; private set => SetProperty(ref _scanText, value); }

    public string CpuName => _scan?.Hardware.CpuName ?? "–";
    public string CpuDetails => _scan is null ? string.Empty
        : $"{_scan.Hardware.CpuCores?.ToString() ?? "?"} cores · {_scan.Hardware.CpuThreads?.ToString() ?? "?"} threads";
    public string GpuName => _scan?.Hardware.PrimaryGpuName ?? "–";
    public string GpuDetails => _scan is null ? string.Empty
        : (_scan.Profile.HasDedicatedGpu ? "Dedicated graphics" : _scan.Hardware.Gpus.Any(g => g.IsIntegrated) ? "Integrated graphics" : "Graphics");
    public string RamText => _scan?.Hardware.TotalRamBytes is long r ? $"{Math.Round(r / 1024d / 1024 / 1024)} GB" : Formatting.Unavailable;
    public string RamDetails => _scan?.Hardware.RamSpeedMhz is int s ? $"{s} MT/s · {_scan.Hardware.RamModules} module(s)" : string.Empty;
    public string WindowsName => _scan?.Hardware.WindowsName ?? "–";
    public string WindowsVersion => _scan?.Hardware.WindowsVersion ?? string.Empty;
    public string DeviceTitle => _scan?.Profile.Title ?? string.Empty;
    public string DeviceDescription => _scan?.Profile.Description ?? string.Empty;
    public bool IsLowEnd => _scan?.Profile.IsLowPowerIntegrated ?? false;
    public string DeviceModel => _scan is null ? string.Empty : $"{_scan.Hardware.Manufacturer} {_scan.Hardware.Model}";

    public int Score => _scan?.Score ?? 0;
    public PerformanceStatus Status => _scan?.Status ?? PerformanceStatus.CouldBeImproved;
    public string StatusText => Status switch
    {
        PerformanceStatus.Good => "Good",
        PerformanceStatus.CouldBeImproved => "Could be improved",
        _ => "Needs attention",
    };
    public string StatusEmoji => Status switch
    {
        PerformanceStatus.Good => "🟢",
        PerformanceStatus.CouldBeImproved => "🟡",
        _ => "🔴",
    };

    public string PowerText => _scan is null ? "–" : _scan.State.PowerPlanName + (_scan.State.PowerModeName is "–" or "Unavailable" ? string.Empty : " · " + _scan.State.PowerModeName);
    public string GameModeText => _scan?.State.GameModeEnabled switch { true => "ON", false => "OFF", _ => "Unavailable" };
    public string GameBarText => _scan?.State.GameDvrEnabled switch { true => "Captures ON", false => "Captures OFF", _ => "Unavailable" };
    public string HagsText => _scan?.State.HagsText ?? "–";
    public string StartupText => _scan is null ? "–" : $"{_scan.State.EnabledStartupApps} enabled";

    public string CpuUsage { get => _cpuUsage; private set => SetProperty(ref _cpuUsage, value); }
    public string GpuUsage { get => _gpuUsage; private set => SetProperty(ref _gpuUsage, value); }
    public string RamUsage { get => _ramUsage; private set => SetProperty(ref _ramUsage, value); }
    public string ThermalText { get => _thermalText; private set => SetProperty(ref _thermalText, value); }

    private string _cpuTempText = "Unavailable";
    private string _gpuTempText = "Unavailable";
    private string _tempSourceText = string.Empty;

    public string CpuTempText { get => _cpuTempText; private set => SetProperty(ref _cpuTempText, value); }
    public string GpuTempText { get => _gpuTempText; private set => SetProperty(ref _gpuTempText, value); }
    public string TempSourceText { get => _tempSourceText; private set => SetProperty(ref _tempSourceText, value); }

    public override void OnNavigatedTo()
    {
        if (!_subscribed)
        {
            MonitorService.Instance.Sampled += OnSample;
            MonitorService.Instance.Subscribe();
            _subscribed = true;
        }

        if (ScanService.Instance.Last is { } last)
        {
            Apply(last);
        }
        else if (!IsScanning && !(MainViewModel.Instance?.ShowWelcome ?? false))
        {
            _ = ScanAsync();
        }
    }

    public override void OnNavigatedFrom()
    {
        if (_subscribed)
        {
            MonitorService.Instance.Sampled -= OnSample;
            MonitorService.Instance.Unsubscribe();
            _subscribed = false;
        }
    }

    private void OnSample(MonitorSnapshot s)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            CpuUsage = Formatting.Percent(s.CpuPercent);
            GpuUsage = MonitorService.Instance.GpuAvailable ? Formatting.Percent(s.GpuPercent) : Formatting.Unavailable;
            RamUsage = Formatting.Percent(s.RamPercent);
            ThermalText = s.Thermal is null ? "Unavailable" : $"{Math.Round(s.Thermal.Celsius)} °C";
            CpuTempText = s.Sensors.CpuTemperature is double c ? $"{c:0} °C" : "Unavailable";
            GpuTempText = s.Sensors.GpuTemperature is double g ? $"{g:0} °C" : "Unavailable";
            TempSourceText = s.Sensors.Provider is { } p ? $"CPU/GPU values read from {p}." : string.Empty;
        });
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            await ScanService.Instance.ScanAsync(new Progress<string>(t => ScanText = t));
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Apply(ScanResult result)
    {
        _scan = result;
        Checks.Clear();
        foreach (var c in result.Checks)
        {
            Checks.Add(c);
        }

        foreach (string name in new[]
                 {
                     nameof(HasScan), nameof(CpuName), nameof(CpuDetails), nameof(GpuName), nameof(GpuDetails), nameof(RamText),
                     nameof(RamDetails), nameof(WindowsName), nameof(WindowsVersion), nameof(DeviceTitle), nameof(DeviceDescription),
                     nameof(IsLowEnd), nameof(DeviceModel), nameof(Score), nameof(Status), nameof(StatusText), nameof(StatusEmoji),
                     nameof(PowerText), nameof(GameModeText), nameof(GameBarText), nameof(HagsText), nameof(StartupText),
                 })
        {
            OnPropertyChanged(name);
        }
    }
}
