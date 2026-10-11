using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ConstiBoost.Fortnite;
using ConstiBoost.Models;

namespace ConstiBoost.Services;

/// <summary>
/// Keeps a 15-minute temperature/load history and raises calm in-app alerts when the temperature
/// stays very high. Runs while the Thermal page is open or "Thermal Monitoring" is enabled.
/// </summary>
public sealed class ThermalMonitorService
{
    public const int HistorySeconds = 15 * 60;

    private static readonly HashSet<string> GameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        FortniteLocator.ProcessName, "VALORANT-Win64-Shipping", "cs2", "r5apex", "GTA5", "RobloxPlayerBeta",
        "League of Legends", "RocketLeague", "FallGuys_client_game", "Overwatch", "destiny2", "eldenring",
        "Minecraft.Windows", "javaw", "FC24", "FC25", "FC26", "cod", "ModernWarfare", "TslGame",
    };

    private readonly object _gate = new();
    private readonly List<ThermalSample> _history = new();
    private int _subscriptions;
    private DateTime? _highSince;
    private DateTime? _criticalSince;
    private DateTime _lastHighAlert = DateTime.MinValue;
    private DateTime _lastCriticalAlert = DateTime.MinValue;
    private DateTime _lastRiseAlert = DateTime.MinValue;
    private DateTime _lastGameCheck = DateTime.MinValue;
    private bool _gameRunning;
    private string? _runningGame;

    public static ThermalMonitorService Instance { get; } = new();

    public event Action<ThermalSample>? SampleAdded;

    public event Action? AlertChanged;

    public ThermalAlertLevel Alert { get; private set; }

    public string? RunningGame => _runningGame;

    public bool IsAutomaticMonitoring { get; private set; }

    public IReadOnlyList<ThermalSample> History
    {
        get
        {
            lock (_gate)
            {
                return _history.ToList();
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_subscriptions++ == 0)
            {
                MonitorService.Instance.Sampled += OnSample;
                MonitorService.Instance.Subscribe();
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_subscriptions == 0)
            {
                return;
            }

            if (--_subscriptions == 0)
            {
                MonitorService.Instance.Sampled -= OnSample;
                MonitorService.Instance.Unsubscribe();
            }
        }
    }

    /// <summary>"Enable Thermal Monitoring": keeps measuring every second in the background.</summary>
    public void SetAutomaticMonitoring(bool enabled)
    {
        if (enabled == IsAutomaticMonitoring)
        {
            return;
        }

        IsAutomaticMonitoring = enabled;
        if (enabled)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private static HashSet<string> _libraryGames = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsGameProcess(string name) => GameProcesses.Contains(name) || _libraryGames.Contains(name);

    /// <summary>Main executables of the games found in the library (so any installed game counts as "gaming load").</summary>
    public static void RegisterLibraryGames(IEnumerable<string> executableNames) =>
        _libraryGames = new HashSet<string>(executableNames, StringComparer.OrdinalIgnoreCase);

    private void OnSample(MonitorSnapshot s)
    {
        if (DateTime.UtcNow - _lastGameCheck > TimeSpan.FromSeconds(5))
        {
            _lastGameCheck = DateTime.UtcNow;
            DetectGame();
        }

        var sample = new ThermalSample(s.Timestamp, s.Sensors.BestTemperature, s.Sensors.HasDirectCpuSensor, s.CpuPercent, s.GpuPercent, s.RamPercent, s.CpuMhz, _gameRunning);
        lock (_gate)
        {
            _history.Add(sample);
            var cutoff = DateTime.Now.AddSeconds(-HistorySeconds);
            int remove = _history.FindIndex(h => h.Time >= cutoff);
            if (remove > 0)
            {
                _history.RemoveRange(0, remove);
            }
        }

        EvaluateAlerts(sample);
        AutoCoolService.Instance.Evaluate(sample);
        SampleAdded?.Invoke(sample);
    }

    private void DetectGame()
    {
        try
        {
            string? found = null;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    if (found is null && IsGameProcess(p.ProcessName))
                    {
                        found = p.ProcessName;
                    }
                }
            }

            _runningGame = found;
            _gameRunning = found != null;
        }
        catch
        {
            // keep last state
        }
    }

    private void EvaluateAlerts(ThermalSample sample)
    {
        var now = sample.Time;
        double? t = sample.Temperature;
        var previous = Alert;

        var settings = SettingsService.Instance.Current;
        int high = Math.Clamp(settings.ThermalHighThreshold, 70, 95);
        int critical = Math.Clamp(settings.ThermalCriticalThreshold, high + 1, 100);
        int sustain = Math.Clamp(settings.ThermalSustainSeconds, 15, 120);
        _highSince = t >= high ? _highSince ?? now : null;
        _criticalSince = t >= critical ? _criticalSince ?? now : null;

        if (_criticalSince.HasValue && now - _criticalSince.Value >= TimeSpan.FromSeconds(10))
        {
            Alert = ThermalAlertLevel.Critical;
            if (now - _lastCriticalAlert > TimeSpan.FromMinutes(5))
            {
                _lastCriticalAlert = now;
                NotificationService.Instance.Show("🔴 CRITICAL TEMPERATURE – The system may be under excessive thermal load. Consider stopping the workload and checking cooling.", NotificationKind.Error);
            }
        }
        else if (_highSince.HasValue && now - _highSince.Value >= TimeSpan.FromSeconds(sustain))
        {
            Alert = ThermalAlertLevel.High;
            if (now - _lastHighAlert > TimeSpan.FromMinutes(10))
            {
                _lastHighAlert = now;
                NotificationService.Instance.Show("🟠 HIGH TEMPERATURE – Your CPU temperature is currently high. Check CPU load and cooling conditions.", NotificationKind.Warning);
            }
        }
        else if (t is null || t < high - 3)
        {
            Alert = ThermalAlertLevel.None; // small hysteresis
        }

        // Unusual rise: +15 °C within 60 s while already warm.
        if (t >= 75 && now - _lastRiseAlert > TimeSpan.FromMinutes(10))
        {
            ThermalSample? minuteAgo;
            lock (_gate)
            {
                minuteAgo = _history.LastOrDefault(h => h.Time <= now.AddSeconds(-60) && h.Temperature.HasValue);
            }

            if (minuteAgo?.Temperature is double old && t - old >= 15)
            {
                _lastRiseAlert = now;
                NotificationService.Instance.Show($"Temperature rose from {old:0} °C to {t:0} °C within a minute. Check what started running (Thermal page → SCAN MY PC).", NotificationKind.Warning);
            }
        }

        if (previous != Alert)
        {
            AlertChanged?.Invoke();
        }
    }

    /// <summary>Per-minute summaries so phases like Idle → Gaming → Heavy load → Idle are visible.</summary>
    public List<MinuteSummary> MinuteSummaries()
    {
        var history = History;
        return history
            .GroupBy(h => new DateTime(h.Time.Year, h.Time.Month, h.Time.Day, h.Time.Hour, h.Time.Minute, 0))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var temps = g.Where(x => x.Temperature.HasValue).Select(x => x.Temperature!.Value).ToList();
                var loads = g.Where(x => x.CpuLoad.HasValue).Select(x => x.CpuLoad!.Value).ToList();
                double? load = loads.Count > 0 ? loads.Average() : null;
                bool game = g.Any(x => x.GameRunning);
                string phase = game ? "Gaming" : load switch
                {
                    null => "–",
                    < 15 => "Idle",
                    < 40 => "Light load",
                    < 70 => "Medium load",
                    _ => "Heavy load",
                };
                return new MinuteSummary(
                    g.Key.ToString("HH:mm"),
                    temps.Count > 0 ? $"{temps.Average():0} °C (max {temps.Max():0})" : "Sensor unavailable",
                    load.HasValue ? $"{load:0} %" : "–",
                    phase);
            })
            .ToList();
    }
}
