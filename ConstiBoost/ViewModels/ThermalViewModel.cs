using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Fortnite;
using ConstiBoost.Hardware;
using ConstiBoost.Localization;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.ViewModels;

public sealed record ComparisonLine(string Profile, string AvgTemp, string MaxTemp, string AvgClock, string AvgLoad, string Time, string Fps = "–");

/// <summary>🌡️ Thermal Monitor: live values, diagnosis, safe thermal optimization, tests and cooling advice.</summary>
public sealed class ThermalViewModel : PageViewModel
{
    private const int ChartSeconds = 300;
    private readonly ThermalMonitorService _monitor = ThermalMonitorService.Instance;
    private bool _active;
    private int _uiTick;
    private CancellationTokenSource? _cts;

    private string _cpuTemp = L.T("Sensor unavailable");
    private TemperatureLevel _level = TemperatureLevel.Unavailable;
    private string _tempSource = string.Empty;
    private string _gpuTemp = L.T("Sensor unavailable");
    private string _acpiText = L.T("Sensor unavailable");
    private string _cpuLoad = "–";
    private string _gpuLoad = "–";
    private string _ram = "–";
    private string _cpuClock = L.T("Unavailable");
    private string _gpuClock = L.T("Unavailable");
    private string _fan = L.T("Fan speed unavailable on this device.");
    private string _power = "–";
    private string _processes = "–";
    private IReadOnlyList<double> _tempHistory = Array.Empty<double>();
    private IReadOnlyList<double> _loadHistory = Array.Empty<double>();

    private bool _isBusy;
    private string _progress = string.Empty;
    private ThermalScanResult? _scan;
    private bool _showFindings;
    private ThermalMeasurement? _before;
    private ThermalMeasurement? _after;
    private bool _testRunning;
    private int _testSeconds;
    private ThermalTestRecord? _test;
    private string _preTest = string.Empty;

    public ThermalViewModel()
    {
        ScanCommand = new AsyncRelayCommand(() => ScanAsync(false), () => !IsBusy && !TestRunning);
        FindWaysCommand = new AsyncRelayCommand(() => ScanAsync(true), () => !IsBusy && !TestRunning);
        OptimizeCommand = new AsyncRelayCommand(OptimizeAsync, () => !IsBusy && !TestRunning && Findings.Any(f => f.IsSelected));
        TestCommand = new AsyncRelayCommand(RunTestAsync, () => !IsBusy && !TestRunning);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy || TestRunning);
        _monitor.AlertChanged += () => Application.Current?.Dispatcher.BeginInvoke(RaiseAlert);
        AutoCoolService.Instance.Changed += () => Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(AutoCoolStatus)));
        LoadComparison();
    }

    public override string Key => "thermal";
    public override string Title => Localization.L.T("Thermal");
    public override string Icon => "";

    public ICommand ScanCommand { get; }
    public ICommand FindWaysCommand { get; }
    public ICommand OptimizeCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand CancelCommand { get; }

    public GamingModeViewModel GamingMode => GamingModeViewModel.Shared;

    public ObservableCollection<MinuteSummary> Minutes { get; } = new();
    public ObservableCollection<ThermalCause> Causes { get; } = new();
    public ObservableCollection<ProcessUsage> TopCpu { get; } = new();
    public ObservableCollection<ProcessUsage> TopRam { get; } = new();
    public ObservableCollection<ProcessUsage> TopGpu { get; } = new();
    public ObservableCollection<string> Recommendations { get; } = new();
    public ObservableCollection<ThermalFinding> Findings { get; } = new();
    public ObservableCollection<ComparisonLine> Comparison { get; } = new();
    public ObservableCollection<string> ComparisonStatements { get; } = new();
    public ObservableCollection<string> FortniteRecommendations { get; } = new();

    // ---------- Live ----------
    public string CpuTemp { get => _cpuTemp; private set => SetProperty(ref _cpuTemp, value); }
    public TemperatureLevel Level
    {
        get => _level;
        private set
        {
            if (SetProperty(ref _level, value))
            {
                OnPropertyChanged(nameof(LevelText));
                OnPropertyChanged(nameof(LevelStatus));
            }
        }
    }

    public string LevelText => TemperatureLevels.Text(Level);
    public CheckStatus LevelStatus => TemperatureLevels.Status(Level);
    public string TempSource { get => _tempSource; private set => SetProperty(ref _tempSource, value); }
    public string GpuTemp { get => _gpuTemp; private set => SetProperty(ref _gpuTemp, value); }
    public string AcpiText { get => _acpiText; private set => SetProperty(ref _acpiText, value); }
    public string CpuLoad { get => _cpuLoad; private set => SetProperty(ref _cpuLoad, value); }
    public string GpuLoad { get => _gpuLoad; private set => SetProperty(ref _gpuLoad, value); }
    public string Ram { get => _ram; private set => SetProperty(ref _ram, value); }
    public string CpuClock { get => _cpuClock; private set => SetProperty(ref _cpuClock, value); }
    public string GpuClock { get => _gpuClock; private set => SetProperty(ref _gpuClock, value); }
    public string Fan { get => _fan; private set => SetProperty(ref _fan, value); }
    public string Power { get => _power; private set => SetProperty(ref _power, value); }
    public string Processes { get => _processes; private set => SetProperty(ref _processes, value); }
    public IReadOnlyList<double> TempHistory { get => _tempHistory; private set => SetProperty(ref _tempHistory, value); }
    public IReadOnlyList<double> LoadHistory { get => _loadHistory; private set => SetProperty(ref _loadHistory, value); }

    public bool ThermalMonitoringEnabled
    {
        get => SettingsService.Instance.Current.ThermalMonitoringEnabled;
        set
        {
            SettingsService.Instance.Current.ThermalMonitoringEnabled = value;
            SettingsService.Instance.Save();
            _monitor.SetAutomaticMonitoring(value || S.AutoCoolEnabled);
            OnPropertyChanged();
        }
    }

    // ---------- Thermal settings ----------
    private AppSettings S => SettingsService.Instance.Current;

    public int HighThreshold { get => S.ThermalHighThreshold; set { S.ThermalHighThreshold = Math.Clamp(value, 70, 95); if (S.ThermalCriticalThreshold <= S.ThermalHighThreshold) S.ThermalCriticalThreshold = S.ThermalHighThreshold + 5; SaveSettings(); OnPropertyChanged(nameof(CriticalThreshold)); } }
    public int CriticalThreshold { get => S.ThermalCriticalThreshold; set { S.ThermalCriticalThreshold = Math.Clamp(value, S.ThermalHighThreshold + 1, 100); SaveSettings(); } }
    public int SustainSeconds { get => S.ThermalSustainSeconds; set { S.ThermalSustainSeconds = Math.Clamp(value, 15, 120); SaveSettings(); } }
    public bool DeepScan { get => S.ThermalDeepScan; set { S.ThermalDeepScan = value; SaveSettings(); } }

    public bool AutoCoolEnabled
    {
        get => S.AutoCoolEnabled;
        set
        {
            if (value && !S.AutoCoolEnabled && !DialogService.Confirm("Enable Auto-Cool?",
                    "When the temperature stays above the trigger for 60 seconds, ConstiBoost switches to the Balanced power mode (and optionally turns off CPU boost). After 2 minutes of lower temperature the previous settings are restored. Fans, voltages and thermal protection are never touched. Thermal Monitoring runs in the background while Auto-Cool is on.",
                    null, "ENABLE"))
            {
                OnPropertyChanged();
                return;
            }

            bool wasMonitoring = S.ThermalMonitoringEnabled || S.AutoCoolEnabled;
            S.AutoCoolEnabled = value;
            SaveSettings();
            bool monitor = S.ThermalMonitoringEnabled || S.AutoCoolEnabled;
            if (monitor != wasMonitoring)
            {
                _monitor.SetAutomaticMonitoring(monitor);
            }

            if (!value && AutoCoolService.Instance.IsEngaged)
            {
                _ = AutoCoolService.Instance.DisengageAsync("Auto-Cool turned off");
            }

            OnPropertyChanged();
        }
    }

    public int AutoCoolTrigger { get => S.AutoCoolTrigger; set { S.AutoCoolTrigger = Math.Clamp(value, 75, 95); SaveSettings(); } }
    public bool AutoCoolDuringGaming { get => S.AutoCoolDuringGaming; set { S.AutoCoolDuringGaming = value; SaveSettings(); } }
    public bool AutoCoolLimitBoost { get => S.AutoCoolLimitBoost; set { S.AutoCoolLimitBoost = value; SaveSettings(); } }
    public string AutoCoolStatus => AutoCoolService.Instance.IsEngaged ? L.F("❄️ Auto-Cool ENGAGED since {0:HH:mm}", AutoCoolService.Instance.EngagedAt) : S.AutoCoolEnabled ? L.T("Auto-Cool armed – watching the temperature") : L.T("Auto-Cool off");

    private void SaveSettings()
    {
        SettingsService.Instance.Save();
        OnPropertyChanged(string.Empty);
    }

    // ---------- Alerts ----------
    public bool HasAlert => _monitor.Alert != ThermalAlertLevel.None;
    public bool IsCriticalAlert => _monitor.Alert == ThermalAlertLevel.Critical;
    public string AlertTitle => _monitor.Alert == ThermalAlertLevel.Critical ? L.T("🔴 CRITICAL TEMPERATURE") : L.T("🟠 HIGH TEMPERATURE");
    public string AlertText => _monitor.Alert == ThermalAlertLevel.Critical
        ? L.T("The system may be under excessive thermal load. Consider stopping the workload and checking cooling.")
        : L.T("Your CPU temperature is currently high. Check CPU load and cooling conditions.");

    // ---------- CPU detection ----------
    public string CpuDetected
    {
        get
        {
            var hw = ScanService.Instance.Last?.Hardware;
            if (hw is null)
            {
                return string.Empty;
            }

            string shortName = CpuDatabase.ShortName(hw.CpuName);
            return L.F("{0} detected.", shortName);
        }
    }

    public string CpuName => ScanService.Instance.Last?.Hardware is { } hw ? CpuDatabase.ShortName(hw.CpuName) : "–";
    public string CpuArchitecture => ScanService.Instance.Last?.Hardware is { } hw ? CpuDatabase.Architecture(hw.CpuName) ?? L.T("Unknown") : "–";
    public string CpuCores => ScanService.Instance.Last?.Hardware.CpuCores?.ToString() ?? Formatting.Unavailable;
    public string CpuThreads => ScanService.Instance.Last?.Hardware.CpuThreads?.ToString() ?? Formatting.Unavailable;
    public string CpuBase => ScanService.Instance.Last?.Hardware.CpuMaxClockMhz is int mhz ? L.F("{0} MHz (reported by Windows)", mhz) : Formatting.Unavailable;

    // ---------- Scan ----------
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ProgressText { get => _progress; private set => SetProperty(ref _progress, value); }
    public bool HasScan => _scan != null;
    public bool ShowFindings { get => _showFindings; private set => SetProperty(ref _showFindings, value); }

    public string ScanTemperature => _scan is null ? "–" : T(_scan.Measurement.AverageTemperature, "Sensor unavailable");
    public string ScanTempRange => _scan?.Measurement.AverageTemperature is null ? string.Empty
        : L.F("now {0} · avg {1} · max {2}", T(_scan.Measurement.FinalTemperature), T(_scan.Measurement.AverageTemperature), T(_scan.Measurement.MaxTemperature));
    public string ScanCpuLoad => _scan is null ? "–" : P(_scan.Measurement.AverageCpuLoad);
    public string ScanCpuClock => _scan?.Measurement.AverageCpuMhz is double mhz ? L.F("{0:0.00} GHz avg", mhz / 1000) : Formatting.Unavailable;
    public string ScanGpu => _scan is null ? "–" : L.F("{0} · Temp {1}", P(_scan.GpuLoadAverage), T(_scan.GpuTemperature, "Sensor unavailable"));
    public string ScanVram => _scan?.GpuMemory is { } g ? L.F("Dedicated {0} · Shared {1}", Formatting.Bytes(g.DedicatedBytes), Formatting.Bytes(g.SharedBytes)) : Formatting.Unavailable;
    public string ScanRam => _scan is null || _scan.RamTotal == 0 ? "–"
        : L.F("{0} used · {1} free · {2} total", Formatting.Bytes(_scan.RamUsed), Formatting.Bytes(_scan.RamTotal - _scan.RamUsed), Formatting.Bytes(_scan.RamTotal));
    public string ScanSource => _scan?.TemperatureSource ?? string.Empty;
    public string MainCause => _scan?.MainCause ?? "–";
    public int RecommendationCount => _scan?.Findings.Count ?? 0;
    public string Potential => _scan is null ? "–" : L.T(_scan.Potential.ToString());
    public ImpactLevel PotentialLevel => _scan?.Potential ?? ImpactLevel.Low;
    public int Score => _scan?.Score ?? 0;
    public string ScoreText => _scan is null ? "–" : $"{_scan.Score}/100" + (_scan.ScoreIsPartial ? L.T(" (partial)") : string.Empty);
    public string ScoreSummary => _scan?.ScoreSummary ?? string.Empty;
    public string ScoreTemperature => _scan is null ? "–" : T(_scan.Measurement.AverageTemperature, "Sensor unavailable");
    public string ScoreCooling => _scan is null ? "–"
        : _scan.Fans.Count > 0 ? L.F("Fan active ({0})", string.Join(", ", _scan.Fans.Select(f => $"{f.Value:0} RPM"))) : L.T("Unknown");
    public string ScoreBackground => _scan is null ? "–" : $"{L.T(_scan.BackgroundLevel)} ({_scan.BackgroundCpu:0.0} % CPU)";
    public string ScanPower => _scan?.PowerText ?? "–";
    public string FoundText => Findings.Count == 0
        ? L.T("No safe software measures found – the current load and settings already look reasonable.")
        : L.F("We found {0} potentially useful optimization(s) and {1} piece(s) of advice.", Findings.Count(f => f.CanApply), Findings.Count(f => !f.CanApply));

    // ---------- Before / after ----------
    public bool HasResult => _before != null && _after != null;
    public string BeforeTemp => T(_before?.AverageTemperature, "Sensor unavailable");
    public string AfterTemp => T(_after?.AverageTemperature, "Sensor unavailable");
    public string TempDifference => _before?.AverageTemperature is double b && _after?.AverageTemperature is double a ? $"{a - b:+0.0;-0.0;0.0} °C" : "–";
    public string CpuBefore => P(_before?.AverageCpuLoad);
    public string CpuAfter => P(_after?.AverageCpuLoad);
    public string RamBefore => P(_before?.RamPercent);
    public string RamAfter => P(_after?.RamPercent);
    public string BgBefore => _before?.BackgroundProcesses?.ToString() ?? "–";
    public string BgAfter => _after?.BackgroundProcesses?.ToString() ?? "–";

    public string ResultVerdict
    {
        get
        {
            if (_before is null || _after is null)
            {
                return string.Empty;
            }

            if (_before.AverageTemperature is double b && _after.AverageTemperature is double a)
            {
                double diff = a - b;
                return diff <= -2
                    ? L.F("Temperature measured {0:0.0} °C lower than before. Workload and room temperature also influence this value.", Math.Abs(diff))
                    : L.T("No measurable temperature improvement detected.") + (diff >= 2 ? L.T(" The temperature was higher during the second measurement – the workload may have changed.") : string.Empty);
            }

            return L.T("Thermal sensor unavailable – only CPU load, RAM and background processes can be compared.");
        }
    }

    // ---------- Thermal test ----------
    public bool TestRunning
    {
        get => _testRunning;
        private set
        {
            if (SetProperty(ref _testRunning, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TestSeconds { get => _testSeconds; private set => SetProperty(ref _testSeconds, value); }
    public string PreTest { get => _preTest; private set => SetProperty(ref _preTest, value); }
    public bool HasTest => _test != null;
    public string TestStart => T(_test?.StartTemperature, "Thermal sensor unavailable.");
    public string TestAverage => T(_test?.AverageTemperature, "–");
    public string TestMax => T(_test?.MaxTemperature, "–");
    public string TestFinal => T(_test?.FinalTemperature, "–");
    public string TestLoad => P(_test?.AverageCpuLoad);
    public string TestClock => _test?.AverageCpuMhz is double mhz ? $"{mhz / 1000:0.00} GHz" : Formatting.Unavailable;
    public string TestInfo => _test is null ? string.Empty : $"{L.T(_test.Profile)} · {_test.Time:HH:mm} · {_test.Source}" + (_test.GameRunning ? L.T(" · game running") : string.Empty);

    // ---------- Fortnite thermal mode ----------
    public bool IsFortniteRunning => string.Equals(_monitor.RunningGame, FortniteLocator.ProcessName, StringComparison.OrdinalIgnoreCase);
    public string FpsText => L.T("FPS: unavailable – no supported FPS source (ConstiBoost does not hook into the game).");

    public string FanCheck => _fan;

    // ---------- Lifecycle ----------
    public override void OnNavigatedTo()
    {
        if (!_active)
        {
            _active = true;
            _monitor.SampleAdded += OnSample;
            _monitor.Start();
        }

        if (ScanService.Instance.Last is null)
        {
            _ = ScanService.Instance.ScanAsync().ContinueWith(_ => Application.Current?.Dispatcher.BeginInvoke(RaiseCpuInfo));
        }

        RaiseCpuInfo();
        RefreshPower();
        RaiseAlert();
        RefreshMinutes();
    }

    public override void OnNavigatedFrom()
    {
        if (_active)
        {
            _active = false;
            _monitor.SampleAdded -= OnSample;
            _monitor.Stop();
        }
    }

    private void OnSample(ThermalSample sample)
    {
        var snapshot = MonitorService.Instance.Last;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (snapshot is null)
            {
                return;
            }

            var sensors = snapshot.Sensors;
            double? temp = sensors.BestTemperature;
            CpuTemp = temp.HasValue ? $"{temp:0} °C" : L.T("Sensor unavailable");
            Level = TemperatureLevels.Classify(temp);
            TempSource = sensors.BestTemperatureSource;
            GpuTemp = sensors.GpuTemperature is double g ? $"{g:0} °C" : L.T("Sensor unavailable");
            AcpiText = sensors.AcpiZone is { } z ? $"{z.Celsius:0} °C ({z.Zone})" : L.T("Sensor unavailable");
            CpuLoad = Formatting.Percent(snapshot.CpuPercent);
            GpuLoad = MonitorService.Instance.GpuAvailable ? Formatting.Percent(snapshot.GpuPercent) : L.T("Sensor unavailable");
            Ram = snapshot.RamTotalBytes > 0 ? $"{Formatting.Percent(snapshot.RamPercent)} · {Formatting.Bytes(snapshot.RamUsedBytes)} / {Formatting.Bytes(snapshot.RamTotalBytes)}" : "–";
            CpuClock = snapshot.CpuMhz is double mhz ? $"{mhz / 1000:0.00} GHz" : L.T("Unavailable");
            GpuClock = sensors.GpuClockMhz is double gc ? $"{gc:0} MHz" : L.T("Unavailable");
            Fan = sensors.Fans.Count > 0 ? string.Join(" · ", sensors.Fans.Select(f => $"{f.Name}: {f.Value:0} RPM")) : L.T("Fan speed unavailable on this device.");
            OnPropertyChanged(nameof(FanCheck));
            Processes = snapshot.ProcessCount?.ToString() ?? "–";

            var history = _monitor.History;
            var recent = history.Skip(Math.Max(0, history.Count - ChartSeconds)).ToList();
            TempHistory = recent.Where(h => h.Temperature.HasValue).Select(h => h.Temperature!.Value).ToArray();
            LoadHistory = recent.Where(h => h.CpuLoad.HasValue).Select(h => h.CpuLoad!.Value).ToArray();

            if (_uiTick++ % 5 == 0)
            {
                RefreshPower();
                RefreshMinutes();
                UpdateFortnite(snapshot);
            }
        });
    }

    private void RefreshPower()
    {
        var scheme = PowerManager.GetActiveSchemeId();
        var overlay = scheme == PowerManager.Balanced ? PowerManager.GetOverlay() : null;
        Power = scheme is null ? Formatting.Unavailable
            : PowerManager.GetFriendlyName(scheme.Value) + (overlay is null ? string.Empty : " · " + PowerManager.OverlayName(overlay));
    }

    private void RefreshMinutes()
    {
        Minutes.Clear();
        foreach (var m in _monitor.MinuteSummaries().Take(15))
        {
            Minutes.Add(m);
        }
    }

    private void UpdateFortnite(MonitorSnapshot s)
    {
        OnPropertyChanged(nameof(IsFortniteRunning));
        FortniteRecommendations.Clear();
        if (!IsFortniteRunning)
        {
            return;
        }

        double? temp = s.Sensors.BestTemperature;
        double bg = _scan?.BackgroundCpu ?? -1;
        FortniteRecommendations.Add(bg < 0
            ? L.T("Background load: run SCAN MY PC once to see what else uses CPU while you play.")
            : bg >= 5 ? L.F("Background load: other apps use about {0:0} % CPU – closing them leaves more headroom for Fortnite and may reduce heat.", bg)
            : L.T("Background load: low – good."));
        FortniteRecommendations.Add(s.GpuPercent >= 90
            ? L.T("Graphics load: the GPU is at its limit. A lower 3D resolution or an FPS cap (e.g. 60) reduces GPU heat; use Performance Mode on integrated graphics.")
            : s.CpuPercent >= 85
                ? L.T("Graphics load: the CPU is the limit. Lower view distance and an FPS cap reduce CPU work and heat.")
                : L.T("Graphics load: neither CPU nor GPU is fully loaded."));
        FortniteRecommendations.Add(Power.Contains("Best performance") || Power.Contains("High performance")
            ? L.T("Power mode: maximum performance is active – highest FPS, but also the most heat. BALANCED in Cool Gaming Mode is a middle ground.")
            : L.F("Power mode: {0}.", Power));
        FortniteRecommendations.Add(temp is null
            ? L.T("Cooling status: no temperature sensor readable.")
            : L.F("Cooling status: {0} ({1:0} °C). High temperatures during gaming can be expected under load.", L.T(TemperatureLevels.Text(TemperatureLevels.Classify(temp))), temp));
    }

    private void RaiseAlert()
    {
        OnPropertyChanged(nameof(HasAlert));
        OnPropertyChanged(nameof(IsCriticalAlert));
        OnPropertyChanged(nameof(AlertTitle));
        OnPropertyChanged(nameof(AlertText));
    }

    private void RaiseCpuInfo()
    {
        foreach (string n in new[] { nameof(CpuDetected), nameof(CpuName), nameof(CpuArchitecture), nameof(CpuCores), nameof(CpuThreads), nameof(CpuBase) })
        {
            OnPropertyChanged(n);
        }
    }

    // ---------- Actions ----------
    private async Task ScanAsync(bool focusFindings)
    {
        if (focusFindings && _scan != null && DateTime.Now - _scan.Time < TimeSpan.FromMinutes(2))
        {
            ShowFindings = true;
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            if (ScanService.Instance.Last is null)
            {
                ProgressText = L.T("Reading hardware…");
                await ScanService.Instance.ScanAsync();
                RaiseCpuInfo();
            }

            var result = await ThermalAnalysisService.ScanAsync(new Progress<string>(t => ProgressText = t), _cts.Token);
            ApplyScan(result);
            ShowFindings = ShowFindings || focusFindings;
            NotificationService.Instance.Show(L.F("Scan complete – main cause: {0}.", L.T(result.MainCause)), NotificationKind.Info);
        }
        catch (OperationCanceledException)
        {
            ProgressText = L.T("Scan cancelled.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Thermal scan failed", ex);
            NotificationService.Instance.Show("The thermal scan couldn't be completed. Details were written to the log.", NotificationKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyScan(ThermalScanResult result)
    {
        _scan = result;
        Fill(Causes, result.Causes);
        Fill(TopCpu, result.TopCpu);
        Fill(TopRam, result.TopRam);
        Fill(TopGpu, result.TopGpu);
        Fill(Recommendations, result.Recommendations);
        Fill(Findings, result.Findings);
        foreach (string n in new[]
                 {
                     nameof(HasScan), nameof(ScanTemperature), nameof(ScanTempRange), nameof(ScanCpuLoad), nameof(ScanCpuClock), nameof(ScanGpu),
                     nameof(ScanVram), nameof(ScanRam), nameof(ScanSource), nameof(MainCause), nameof(RecommendationCount), nameof(Potential),
                     nameof(PotentialLevel), nameof(Score), nameof(ScoreText), nameof(ScoreSummary), nameof(ScoreTemperature), nameof(ScoreCooling),
                     nameof(ScoreBackground), nameof(ScanPower), nameof(FoundText),
                 })
        {
            OnPropertyChanged(n);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task OptimizeAsync()
    {
        var selected = Findings.Where(f => f.IsSelected && f.CanApply).ToList();
        if (selected.Count == 0 || _scan is null)
        {
            return;
        }

        var details = selected.Select(f => L.F("{0} → {1}  (impact: {2})", L.T(f.Problem), L.T(f.Recommended), L.T(f.ImpactText))).ToList();
        details.Add("Changed settings are backed up first. Closed apps can simply be opened again.");
        details.Add("Afterwards ConstiBoost measures again for 30 seconds and compares the real values.");
        if (!DialogService.Confirm(L.F("We found {0} potentially useful optimization(s).", selected.Count), "Apply these safe measures?", details, "OPTIMIZE TEMPERATURE"))
        {
            return;
        }

        IsBusy = true;
        _cts = new CancellationTokenSource();
        _before = _scan.Measurement;
        _after = null;
        RaiseResult();
        try
        {
            ProgressText = L.T("Creating backup…");
            var backup = BackupSession.Create("Thermal optimization");
            foreach (var f in selected)
            {
                ProgressText = L.F("Applying: {0}…", L.T(f.Problem));
                OperationResult r;
                try
                {
                    r = await f.Apply!(backup);
                }
                catch (Exception ex)
                {
                    AppLog.Error("Thermal finding failed", ex);
                    r = OperationResult.Fail("This optimization couldn't be applied.", Safe.Describe(ex));
                }

                f.ResultSuccess = r.Success;
                f.ResultText = (r.Success ? "✓ " : "✕ ") + L.T(r.Message) + (r.TechnicalDetails is null ? string.Empty : $" ({r.TechnicalDetails})");
                f.IsSelected = false;
            }

            ProgressText = L.T("Letting the system settle…");
            await Task.Delay(5000, _cts.Token);
            _after = await ThermalAnalysisService.MeasureAsync(30, new Progress<(int s, int total)>(p => ProgressText = L.F("Measuring again… {0}/{1} s", p.s, p.total)), _cts.Token);
            RaiseResult();
            MainViewModel.Instance?.Backups.Reload();
            NotificationService.Instance.Show(ResultVerdict, NotificationKind.Info);
        }
        catch (OperationCanceledException)
        {
            ProgressText = L.T("Measurement cancelled – changes were applied.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseResult()
    {
        foreach (string n in new[] { nameof(HasResult), nameof(BeforeTemp), nameof(AfterTemp), nameof(TempDifference), nameof(CpuBefore), nameof(CpuAfter), nameof(RamBefore), nameof(RamAfter), nameof(BgBefore), nameof(BgAfter), nameof(ResultVerdict) })
        {
            OnPropertyChanged(n);
        }
    }

    private async Task RunTestAsync()
    {
        TestRunning = true;
        _cts = new CancellationTokenSource();
        var last = MonitorService.Instance.Last;
        PreTest = L.F("Before: Temperature {0} · CPU load {1}", T(last?.Sensors.BestTemperature, "Thermal sensor unavailable."), P(last?.CpuPercent));
        try
        {
            _test = await ThermalTestService.RunAsync(60, new Progress<(int s, int total)>(p => TestSeconds = p.s), _cts.Token);
            foreach (string n in new[] { nameof(HasTest), nameof(TestStart), nameof(TestAverage), nameof(TestMax), nameof(TestFinal), nameof(TestLoad), nameof(TestClock), nameof(TestInfo) })
            {
                OnPropertyChanged(n);
            }

            LoadComparison();
        }
        catch (OperationCanceledException)
        {
            PreTest = L.T("Test cancelled.");
        }
        finally
        {
            TestRunning = false;
            TestSeconds = 0;
        }
    }

    public void RefreshComparison() => LoadComparison();

    /// <summary>Compares the latest measured test per profile. Only real measurements – FPS are not available.</summary>
    private void LoadComparison()
    {
        ComparisonStatements.Clear();
        var latest = ThermalTestService.Load()
            .GroupBy(r => r.Profile)
            .Select(g => g.OrderByDescending(r => r.Time).First())
            .OrderBy(r => r.Profile)
            .ToList();
        Comparison.Clear();
        foreach (var r in latest)
        {
            Comparison.Add(new ComparisonLine(L.T(r.Profile), T(r.AverageTemperature, "n/a"), T(r.MaxTemperature, "n/a"),
                r.AverageCpuMhz is double mhz ? $"{mhz / 1000:0.00} GHz" : L.T("n/a"), P(r.AverageCpuLoad), r.Time.ToString("dd.MM. HH:mm")));
        }

        // Real FPS measurements (Games → MEASURE REAL FPS) per Gaming Mode profile, with the temperature measured at the same time.
        var fps = FpsHistory.Load().GroupBy(r => (r.GameName, r.Profile)).Select(g => g.OrderByDescending(r => r.Time).First()).OrderBy(r => r.GameName).ThenBy(r => r.Profile).ToList();
        foreach (var r in fps)
        {
            Comparison.Add(new ComparisonLine($"{r.GameName} · {L.T(r.Profile)}", T(r.AverageTemperature, "n/a"), T(r.MaxTemperature, "n/a"),
                r.AverageCpuMhz is double mhz2 ? $"{mhz2 / 1000:0.00} GHz" : L.T("n/a"), "–", r.Time.ToString("dd.MM. HH:mm"), $"{r.AverageFps:0} FPS (1 % {r.Low1:0})"));
        }

        foreach (var game in fps.GroupBy(r => r.GameName).Where(g => g.Count() >= 2))
        {
            var fastest = game.OrderByDescending(r => r.AverageFps).First();
            var coolest = game.Where(r => r.AverageTemperature.HasValue).OrderBy(r => r.AverageTemperature).FirstOrDefault();
            ComparisonStatements.Add(L.F("{0}: {1} gives higher performance ({2:0} FPS measured).", game.Key, L.T(fastest.Profile), fastest.AverageFps));
            if (coolest != null && coolest != fastest)
            {
                ComparisonStatements.Add(L.F("{0}: {1} runs cooler ({2:0} °C at {3:0} FPS). You decide.", game.Key, L.T(coolest.Profile), coolest.AverageTemperature, coolest.AverageFps));
            }
        }

        var withTemp = latest.Where(r => r.AverageTemperature.HasValue).ToList();
        var withClock = latest.Where(r => r.AverageCpuMhz.HasValue).ToList();
        if (latest.Count >= 2)
        {
            if (withClock.Count >= 2)
            {
                var fastest = withClock.OrderByDescending(r => r.AverageCpuMhz).First();
                ComparisonStatements.Add(L.F("{0} gives higher performance potential (highest average CPU clock: {1:0.00} GHz).", L.T(fastest.Profile), fastest.AverageCpuMhz / 1000));
            }

            if (withTemp.Count >= 2)
            {
                var coolest = withTemp.OrderBy(r => r.AverageTemperature).First();
                ComparisonStatements.Add(L.F("{0} runs cooler (lowest average temperature: {1:0} °C).", L.T(coolest.Profile), coolest.AverageTemperature));
            }

            ComparisonStatements.Add(L.T("Only compare tests with a similar workload (e.g. the same game scene). You decide which balance you prefer."));
        }
        else
        {
            ComparisonStatements.Add(L.T("Run the 60 second thermal test in different Gaming Mode profiles (e.g. during a Fortnite match) to compare performance and temperature with real measurements."));
        }
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string T(double? celsius, string fallback = "–") => celsius.HasValue ? $"{celsius:0} °C" : L.T(fallback);

    private static string P(double? percent) => percent.HasValue ? $"{percent:0} %" : "–";
}
