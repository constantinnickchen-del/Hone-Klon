using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class HardwareViewModel : PageViewModel
{
    private const int HistoryLength = 60;
    private readonly Queue<double> _cpu = new();
    private readonly Queue<double> _gpu = new();
    private readonly Queue<double> _ram = new();
    private bool _subscribed;
    private int _selectedTab;
    private string _cpuNow = "–";
    private string _gpuNow = "–";
    private string _ramNow = "–";
    private string _ramDetail = string.Empty;
    private string _thermalNow = "Temperature unavailable";
    private string _thermalZone = string.Empty;
    private IReadOnlyList<double> _cpuHistory = Array.Empty<double>();
    private IReadOnlyList<double> _gpuHistory = Array.Empty<double>();
    private IReadOnlyList<double> _ramHistory = Array.Empty<double>();

    public HardwareViewModel()
    {
        Processes = new ProcessesViewModel();
        Network = new NetworkViewModel();
        Benchmark = new BenchmarkViewModel();
    }

    public override string Key => "hardware";
    public override string Title => Localization.L.T("Hardware");
    public override string Icon => "";

    public ProcessesViewModel Processes { get; }
    public NetworkViewModel Network { get; }
    public BenchmarkViewModel Benchmark { get; }

    public ObservableCollection<KeyValuePair<string, string>> Details { get; } = new();
    public ObservableCollection<DriveSummary> Drives { get; } = new();

    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                UpdateTabActivity();
            }
        }
    }

    public string CpuNow { get => _cpuNow; private set => SetProperty(ref _cpuNow, value); }
    public string GpuNow { get => _gpuNow; private set => SetProperty(ref _gpuNow, value); }
    public string RamNow { get => _ramNow; private set => SetProperty(ref _ramNow, value); }
    public string RamDetail { get => _ramDetail; private set => SetProperty(ref _ramDetail, value); }
    public string ThermalNow { get => _thermalNow; private set => SetProperty(ref _thermalNow, value); }

    private string _cpuTempNow = "Temperature unavailable";
    private string _gpuTempNow = "Temperature unavailable";

    public string CpuTempNow { get => _cpuTempNow; private set => SetProperty(ref _cpuTempNow, value); }
    public string GpuTempNow { get => _gpuTempNow; private set => SetProperty(ref _gpuTempNow, value); }
    public string ThermalZone { get => _thermalZone; private set => SetProperty(ref _thermalZone, value); }
    public IReadOnlyList<double> CpuHistory { get => _cpuHistory; private set => SetProperty(ref _cpuHistory, value); }
    public IReadOnlyList<double> GpuHistory { get => _gpuHistory; private set => SetProperty(ref _gpuHistory, value); }
    public IReadOnlyList<double> RamHistory { get => _ramHistory; private set => SetProperty(ref _ramHistory, value); }

    public string FpsText => "Unavailable – no supported FPS source. ConstiBoost does not inject into games (anti-cheat safe) and never estimates FPS.";

    public override void OnNavigatedTo()
    {
        LoadDetails();
        UpdateTabActivity();
    }

    public override void OnNavigatedFrom()
    {
        SetMonitoring(false);
        Processes.Stop();
    }

    private void UpdateTabActivity()
    {
        bool visible = MainViewModel.Instance?.CurrentPage == this;
        SetMonitoring(visible && SelectedTab == 0);
        if (visible && SelectedTab == 1)
        {
            Processes.Start();
        }
        else
        {
            Processes.Stop();
        }

        if (visible && SelectedTab == 2)
        {
            Network.Activate();
        }
        else
        {
            Network.Deactivate();
        }
    }

    private void SetMonitoring(bool on)
    {
        if (on && !_subscribed)
        {
            MonitorService.Instance.Sampled += OnSample;
            MonitorService.Instance.Subscribe();
            _subscribed = true;
        }
        else if (!on && _subscribed)
        {
            MonitorService.Instance.Sampled -= OnSample;
            MonitorService.Instance.Unsubscribe();
            _subscribed = false;
        }
    }

    private void LoadDetails()
    {
        var scan = ScanService.Instance.Last;
        if (scan is null)
        {
            _ = ScanService.Instance.ScanAsync().ContinueWith(_ => Application.Current?.Dispatcher.BeginInvoke(LoadDetails));
            return;
        }

        var hw = scan.Hardware;
        Details.Clear();
        Details.Add(new("Device", $"{hw.Manufacturer} {hw.Model}"));
        Details.Add(new("CPU", hw.CpuName));
        Details.Add(new("Cores / Threads", $"{hw.CpuCores?.ToString() ?? "?"} / {hw.CpuThreads?.ToString() ?? "?"}"));
        Details.Add(new("Max clock", hw.CpuMaxClockMhz is int c ? $"{c} MHz" : Formatting.Unavailable));
        foreach (var gpu in hw.Gpus)
        {
            Details.Add(new(gpu.IsIntegrated ? "GPU (integrated)" : "GPU", gpu.Name));
            Details.Add(new("  Driver", gpu.DriverVersion));
            if (gpu.HorizontalResolution.HasValue)
            {
                Details.Add(new("  Display", $"{gpu.HorizontalResolution}×{gpu.VerticalResolution} @ {gpu.RefreshRate?.ToString() ?? "?"} Hz"));
            }
        }

        Details.Add(new("RAM", hw.TotalRamBytes is long r ? Formatting.Bytes(r) : Formatting.Unavailable));
        Details.Add(new("RAM speed", hw.RamSpeedMhz is int s ? $"{s} MT/s ({hw.RamModules} module(s){(hw.RamModules == 1 ? " – single channel" : string.Empty)})" : Formatting.Unavailable));
        Details.Add(new("Windows", $"{hw.WindowsName} {hw.WindowsVersion}"));
        Details.Add(new("Power", hw.HasBattery ? $"Battery {hw.BatteryPercent?.ToString() ?? "?"} % · {(hw.OnAcPower == true ? "plugged in" : hw.OnAcPower == false ? "on battery" : "unknown")}" : "Desktop / no battery"));
        Drives.Clear();
        foreach (var d in hw.Drives)
        {
            Drives.Add(d);
        }
    }

    private void OnSample(MonitorSnapshot s)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Push(_cpu, s.CpuPercent);
            Push(_gpu, s.GpuPercent);
            Push(_ram, s.RamPercent);
            CpuHistory = _cpu.ToArray();
            GpuHistory = _gpu.ToArray();
            RamHistory = _ram.ToArray();
            CpuNow = Formatting.Percent(s.CpuPercent);
            GpuNow = MonitorService.Instance.GpuAvailable ? Formatting.Percent(s.GpuPercent) : Formatting.Unavailable;
            RamNow = Formatting.Percent(s.RamPercent);
            RamDetail = s.RamTotalBytes > 0 ? $"{Formatting.Bytes(s.RamUsedBytes)} of {Formatting.Bytes(s.RamTotalBytes)}" : string.Empty;
            CpuTempNow = s.Sensors.CpuTemperature is double ct ? $"{ct:0} °C ({s.Sensors.Provider})" : "Temperature unavailable";
            GpuTempNow = s.Sensors.GpuTemperature is double gt ? $"{gt:0} °C ({s.Sensors.Provider})" : "Temperature unavailable";
            ThermalNow = s.Thermal is null ? "Temperature unavailable" : $"{Math.Round(s.Thermal.Celsius)} °C";
            ThermalZone = s.Thermal is null
                ? "No readable sensor. CPU/GPU die temperatures need vendor drivers, which ConstiBoost does not install."
                : $"ACPI thermal zone {s.Thermal.Zone} – a firmware sensor, not necessarily the CPU die.";
        });
    }

    private static void Push(Queue<double> queue, double? value)
    {
        if (value is null)
        {
            return;
        }

        queue.Enqueue(value.Value);
        while (queue.Count > HistoryLength)
        {
            queue.Dequeue();
        }
    }
}
