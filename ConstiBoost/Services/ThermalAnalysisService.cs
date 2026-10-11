using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Hardware.Sensors;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Optimizers;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed class ThermalScanResult
{
    public DateTime Time { get; init; } = DateTime.Now;
    public ThermalMeasurement Measurement { get; init; } = new();
    public string TemperatureSource { get; init; } = "Sensor unavailable";
    public bool HasDirectSensor { get; init; }
    public double? CpuLoadMax { get; init; }
    public double? GpuLoadAverage { get; init; }
    public double? GpuTemperature { get; init; }
    public GpuMemoryUsage? GpuMemory { get; init; }
    public long RamTotal { get; init; }
    public long RamUsed { get; init; }
    public IReadOnlyList<SensorValue> Fans { get; init; } = Array.Empty<SensorValue>();
    public List<ProcessUsage> TopCpu { get; init; } = new();
    public List<ProcessUsage> TopRam { get; init; } = new();
    public List<ProcessUsage> TopGpu { get; init; } = new();
    public List<ThermalCause> Causes { get; init; } = new();
    public string MainCause { get; init; } = "Unknown";
    public List<ThermalFinding> Findings { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public int Score { get; init; }
    public bool ScoreIsPartial { get; init; }
    public string ScoreSummary { get; init; } = string.Empty;
    public string BackgroundLevel { get; init; } = "–";
    public double BackgroundCpu { get; init; }
    public string PowerText { get; init; } = Formatting.Unavailable;
    public bool PowerAggressive { get; init; }
    public string? RunningGame { get; init; }
    public ImpactLevel Potential { get; init; }
}

/// <summary>
/// Thermal diagnosis: measures for ~10 s, finds the likely cause of heat and lists safe measures in a fixed priority:
/// 1 real high CPU load → 2 background processes → 3 power mode → 4 startup → 5 overlays → 6 cooling advice → 7 optional Windows tweaks.
/// </summary>
public static class ThermalAnalysisService
{
    private const int ScanSeconds = 10;

    public static readonly HashSet<string> OverlayProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameBar", "GameBarFTServer", "XboxGameBarWidgets", "Overwolf", "OverwolfHelper", "OverwolfHelper64", "Medal",
        "NVIDIA Share", "RTSS", "MSIAfterburner", "Outplayed", "PlaysTV",
    };

    private static readonly HashSet<string> MonitoringOverlays = new(StringComparer.OrdinalIgnoreCase) { "RTSS", "MSIAfterburner" };

    public static async Task<ThermalScanResult> ScanAsync(IProgress<string> progress, CancellationToken token)
    {
        return await Task.Run(async () =>
        {
            var processSampler = new ProcessSampler();
            progress.Report("Reading processes…");
            processSampler.Sample(); // baseline for per-process CPU

            var measurement = await MeasureAsync(ScanSeconds, new Progress<(int s, int total)>(p => progress.Report($"Measuring temperature and load… {p.s}/{p.total} s")), token, collectGpu: true);

            progress.Report("Analyzing processes…");
            var samples = processSampler.Sample();
            var usages = Group(samples);

            progress.Report("Checking power mode, startup and overlays…");
            var sensors = SensorHub.Instance.Read();
            var memory = SystemInfo.GetMemoryStatus();
            long ramTotal = memory.HasValue ? (long)memory.Value.total : 0;
            long ramUsed = memory.HasValue ? (long)(memory.Value.total - memory.Value.available) : 0;
            var scheme = PowerManager.GetActiveSchemeId();
            var overlay = scheme == PowerManager.Balanced ? PowerManager.GetOverlay() : null;
            bool aggressive = scheme == PowerManager.HighPerformance || scheme == PowerManager.UltimatePerformance || overlay == PowerManager.OverlayBestPerformance;
            string powerText = scheme is null ? Formatting.Unavailable
                : PowerManager.GetFriendlyName(scheme.Value) + (overlay is null ? string.Empty : " · Power mode: " + PowerManager.OverlayName(overlay));
            string? game = usages.FirstOrDefault(u => u.IsGame)?.Name ?? ThermalMonitorService.Instance.RunningGame;

            double bgCpu = usages.Where(u => u.IsBackground && !u.IsSystem).Sum(u => u.Cpu);
            var startup = Safe.Try(StartupService.GetEntries, new List<StartupEntry>(), "Thermal startup scan");

            var context = ScanService.Instance.Last is { } scan
                ? new OptimizationContext { Hardware = scan.Hardware, Profile = scan.Profile }
                : new OptimizationContext();

            var causes = DetectCauses(measurement, usages, aggressive, game, bgCpu, out string mainCause);
            var findings = BuildFindings(measurement, usages, aggressive, overlay, scheme, game, startup, context);
            var (score, partial, summary) = Score(measurement, bgCpu, aggressive);

            var potential = findings.Any(f => f.CanApply && f.Impact == ImpactLevel.High) ? ImpactLevel.High
                : findings.Any(f => f.CanApply && f.Impact == ImpactLevel.Medium) ? ImpactLevel.Medium
                : ImpactLevel.Low;

            return new ThermalScanResult
            {
                Time = DateTime.Now,
                Measurement = measurement,
                TemperatureSource = sensors.BestTemperatureSource,
                HasDirectSensor = sensors.HasDirectCpuSensor,
                CpuLoadMax = measurement.AverageCpuLoad,
                GpuLoadAverage = measurement.AverageGpuLoad,
                GpuTemperature = sensors.GpuTemperature,
                GpuMemory = GpuMemoryReader.Read(),
                RamTotal = ramTotal,
                RamUsed = ramUsed,
                Fans = sensors.Fans,
                TopCpu = usages.OrderByDescending(u => u.Cpu).Take(8).ToList(),
                TopRam = usages.OrderByDescending(u => u.Ram).Take(8).ToList(),
                TopGpu = usages.Where(u => u.Gpu is > 0.5).OrderByDescending(u => u.Gpu).Take(8).ToList(),
                Causes = causes,
                MainCause = mainCause,
                Findings = findings,
                Recommendations = BuildRecommendations(measurement, usages, bgCpu, aggressive, game),
                Score = score,
                ScoreIsPartial = partial,
                ScoreSummary = summary,
                BackgroundCpu = bgCpu,
                BackgroundLevel = bgCpu < 3 ? "Low" : bgCpu < 10 ? "Medium" : "High",
                PowerText = powerText,
                PowerAggressive = aggressive,
                RunningGame = game,
                Potential = potential,
            };
        }, token);
    }

    /// <summary>Measures real values for the given number of seconds (1 sample per second).</summary>
    public static async Task<ThermalMeasurement> MeasureAsync(int seconds, IProgress<(int s, int total)>? progress, CancellationToken token, bool collectGpu = false)
    {
        var cpu = new CpuUsageSampler();
        var gpu = collectGpu ? new GpuUsageSampler() : null;
        using var freq = new CpuFrequencySampler(ScanService.Instance.Last?.Hardware.CpuMaxClockMhz);
        cpu.Sample();
        gpu?.Sample();
        freq.Sample();

        var temps = new List<double>();
        var loads = new List<double>();
        var clocks = new List<double>();
        var gpuLoads = new List<double>();
        string source = string.Empty;
        double? start = null;
        double? last = null;
        for (int i = 1; i <= seconds; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(1000, token);
            var sensors = SensorHub.Instance.Read();
            double? t = sensors.BestTemperature;
            if (t.HasValue)
            {
                start ??= t;
                last = t;
                temps.Add(t.Value);
                source = sensors.BestTemperatureSource;
            }

            if (cpu.Sample() is double c)
            {
                loads.Add(c);
            }

            if (freq.Sample() is double f)
            {
                clocks.Add(f);
            }

            if (gpu != null)
            {
                gpu.Sample();
                if (gpu.Total is double g)
                {
                    gpuLoads.Add(g);
                }
            }

            progress?.Report((i, seconds));
        }

        var memory = SystemInfo.GetMemoryStatus();
        return new ThermalMeasurement
        {
            Time = DateTime.Now,
            StartTemperature = start,
            AverageTemperature = temps.Count > 0 ? temps.Average() : null,
            MaxTemperature = temps.Count > 0 ? temps.Max() : null,
            FinalTemperature = last,
            AverageCpuLoad = loads.Count > 0 ? loads.Average() : null,
            AverageCpuMhz = clocks.Count > 0 ? clocks.Average() : null,
            AverageGpuLoad = gpuLoads.Count > 0 ? gpuLoads.Average() : null,
            RamPercent = memory?.loadPercent,
            BackgroundProcesses = CountBackgroundProcesses(),
            Source = temps.Count > 0 ? source : "Thermal sensor unavailable",
        };
    }

    /// <summary>User processes without a visible window (excluding Windows/security components).</summary>
    public static int? CountBackgroundProcesses()
    {
        try
        {
            var windows = WindowEnumerator.Snapshot();
            int own = System.Diagnostics.Process.GetCurrentProcess().SessionId;
            int count = 0;
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.SessionId == own && !windows.ContainsKey(p.Id) &&
                            ProcessSafety.Classify(p.ProcessName, null) != ProcessProtection.Critical)
                        {
                            count++;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            return count;
        }
        catch
        {
            return null;
        }
    }

    private static List<ProcessUsage> Group(List<ProcessSample> samples)
    {
        int ownPid = Environment.ProcessId;
        return samples
            .Where(s => s.Pid != ownPid && !s.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                double? gpu = g.Any(x => x.Gpu.HasValue) ? g.Max(x => x.Gpu ?? 0) : null;
                bool system = g.All(x => x.Protection == ProcessProtection.Critical || x.Status == "Service");
                bool background = g.All(x => x.Status is "Background" or "Service");
                return new ProcessUsage(g.Key, g.Sum(x => x.Cpu), g.Sum(x => x.Ram), gpu, g.Count(), background, ThermalMonitorService.IsGameProcess(g.Key), system);
            })
            .ToList();
    }

    private static List<ThermalCause> DetectCauses(ThermalMeasurement m, List<ProcessUsage> usages, bool aggressive, string? game, double bgCpu, out string mainCause)
    {
        var causes = new List<ThermalCause>();
        double load = m.AverageCpuLoad ?? 0;
        double? temp = m.AverageTemperature;
        string? primary = null;

        if (game != null)
        {
            var g = usages.FirstOrDefault(u => u.IsGame);
            causes.Add(new ThermalCause
            {
                Title = "Gaming workload detected.",
                Description = "High temperatures during gaming can be expected because the CPU/GPU are under load. This alone does not mean the temperature is dangerous – laptop CPUs are designed to run warm under load and protect themselves.",
                Severity = CheckStatus.Warning,
                Details = g is null ? new List<string> { game } : new List<string> { $"{g.Name}: CPU {g.CpuText} · RAM {g.RamText} · GPU {g.GpuText}" },
            });
            primary = "High gaming load";
        }

        var heavy = usages.Where(u => !u.IsGame && !u.IsSystem && u.Cpu >= 10).OrderByDescending(u => u.Cpu).ToList();
        if (load >= 60)
        {
            causes.Add(new ThermalCause
            {
                Title = "High CPU load detected.",
                Description = $"The CPU averaged {load:0} % load during the scan. Sustained load is the most common reason for high laptop temperatures.",
                Severity = CheckStatus.Bad,
                Details = usages.Where(u => u.Cpu >= 5).OrderByDescending(u => u.Cpu).Take(6).Select(u => $"{u.Name}: CPU {u.CpuText} · RAM {u.RamText} ({u.Kind})").ToList(),
            });
            primary ??= heavy.Count > 0 ? $"High CPU load ({heavy[0].Name})" : "High CPU load";
        }

        var background = usages.Where(u => !u.IsGame && !u.IsSystem && (u.IsBackground || BackgroundAppsService.KnownBackgroundApps.Contains(u.Name)) && u.Cpu >= 1).OrderByDescending(u => u.Cpu).ToList();
        if (bgCpu >= 5 || background.Any(b => b.Cpu >= 3))
        {
            causes.Add(new ThermalCause
            {
                Title = "Background activity detected.",
                Description = $"Background programs used about {bgCpu:0} % CPU in total during the scan.",
                Severity = CheckStatus.Warning,
                Details = background.Take(6).Select(b => $"{b.Name}: CPU {b.CpuText} · RAM {b.RamText} · Recommendation: close if not needed").ToList(),
            });
            primary ??= "Background process";
        }

        if (aggressive)
        {
            causes.Add(new ThermalCause
            {
                Title = "High-performance power mode may increase heat.",
                Description = "'Best performance' lets the CPU boost higher and longer. That gives the most performance, but also produces more heat. 'Balanced' usually runs cooler with a small performance cost.",
                Severity = CheckStatus.Warning,
            });
            primary ??= "Power mode";
        }

        if (temp >= 75 && load < 25 && (m.AverageCpuLoad.HasValue))
        {
            causes.Add(new ThermalCause
            {
                Title = "Temperature is high despite low CPU load.",
                Description = "Software optimization may have limited impact here. Possible reasons (ConstiBoost cannot verify these):",
                Severity = CheckStatus.Warning,
                Details = new List<string>
                {
                    "Ventilation – laptop on a soft surface or vents covered",
                    "Dust may reduce cooling performance. Consider checking the vents.",
                    "Blocked air intake (bottom of the laptop)",
                    "A possible fan issue (fan not audible under load)",
                    "High room temperature",
                    "A short background process that ran before the scan",
                    "Aging thermal paste (only after several years; a service technician can check it)",
                },
            });
            primary ??= "Cooling conditions (unverified)";
        }

        mainCause = primary ?? (temp is null ? "Unknown (no temperature sensor)" : temp < 75 ? "No thermal issue detected" : "Unknown");
        return causes;
    }

    private static List<ThermalFinding> BuildFindings(ThermalMeasurement m, List<ProcessUsage> usages, bool aggressive, Guid? overlay, Guid? scheme,
        string? game, List<StartupEntry> startup, OptimizationContext context)
    {
        var findings = new List<ThermalFinding>();
        bool gaming = game != null;

        // 1. Real high CPU load (non-system, non-game apps)
        foreach (var u in usages.Where(u => !u.IsGame && !u.IsSystem && u.Cpu >= 10 && !BackgroundAppsService.BrowserProcesses.Contains(u.Name))
                     .OrderByDescending(u => u.Cpu).Take(4))
        {
            bool known = BackgroundAppsService.KnownBackgroundApps.Contains(u.Name);
            var group = ToGroup(u);
            findings.Add(new ThermalFinding
            {
                Priority = 1,
                Category = "High CPU load",
                Problem = $"{u.Name} is using {u.Cpu:0} % CPU",
                Current = $"CPU {u.CpuText} · RAM {u.RamText}",
                Recommended = known ? "Close it while you don't need it" : "Close it if you don't need it right now (you will be asked to save)",
                Impact = u.Cpu >= 20 ? ImpactLevel.High : ImpactLevel.Medium,
                Apply = group is null ? null : _ => CloseAsync(group),
                IsSelected = known && !BackgroundAppsService.KeepPriority.Contains(u.Name),
            });
        }

        // 2. Background processes, launchers, browsers
        foreach (var u in usages.Where(u => !u.IsGame && BackgroundAppsService.KnownBackgroundApps.Contains(u.Name) && u.Cpu < 10 && (u.Cpu >= 0.5 || u.Ram >= 300L * 1024 * 1024))
                     .OrderByDescending(u => u.Cpu).Take(6))
        {
            var group = ToGroup(u);
            bool voice = BackgroundAppsService.KeepPriority.Contains(u.Name);
            bool launcherNeeded = gaming && u.Name.StartsWith("EpicGames", StringComparison.OrdinalIgnoreCase);
            findings.Add(new ThermalFinding
            {
                Priority = 2,
                Category = u.Name.Contains("launcher", StringComparison.OrdinalIgnoreCase) || u.Name.Equals("steam", StringComparison.OrdinalIgnoreCase) ? "Running launcher" : "Background process",
                Problem = $"{u.Name} is running in the background",
                Current = $"CPU {u.CpuText} · RAM {u.RamText}",
                Recommended = voice ? "Close it if you don't need voice/video chat right now" : "Close it while gaming or when not needed",
                Impact = u.Cpu >= 5 ? ImpactLevel.Medium : ImpactLevel.Low,
                Apply = group is null ? null : _ => CloseAsync(group),
                IsSelected = !voice && !launcherNeeded,
            });
        }

        double browserCpu = usages.Where(u => BackgroundAppsService.BrowserProcesses.Contains(u.Name)).Sum(u => u.Cpu);
        long browserRam = usages.Where(u => BackgroundAppsService.BrowserProcesses.Contains(u.Name)).Sum(u => u.Ram);
        int browserProcs = usages.Where(u => BackgroundAppsService.BrowserProcesses.Contains(u.Name)).Sum(u => u.Count);
        if (browserCpu >= 2 || browserRam >= 1024L * 1024 * 1024)
        {
            findings.Add(new ThermalFinding
            {
                Priority = 2,
                Category = "Browser tabs/processes",
                Problem = $"Browser uses {browserCpu:0} % CPU ({browserProcs} processes)",
                Current = $"CPU {browserCpu:0.0} % · RAM {Formatting.Bytes(browserRam)}",
                Recommended = "Close unnecessary tabs (especially video, streams and web games)",
                Impact = browserCpu >= 10 ? ImpactLevel.Medium : ImpactLevel.Low,
            });
        }

        // 3. Power mode
        if (aggressive && scheme.HasValue)
        {
            findings.Add(new ThermalFinding
            {
                Priority = 3,
                Category = "Power mode",
                Problem = "High-performance power mode may increase heat",
                Current = scheme == PowerManager.Balanced ? "Power mode: Best performance" : PowerManager.GetFriendlyName(scheme.Value),
                Recommended = "Balanced power mode",
                Impact = ImpactLevel.Medium,
                ImpactNote = "Lower sustained boost clocks usually mean less heat and fan noise.",
                Tradeoff = "Can reduce FPS slightly in CPU-heavy games. For gaming use Cool Gaming Mode → BALANCED instead.",
                IsReversible = true,
                Apply = backup => Task.Run(() => SetBalanced(backup, scheme.Value, overlay)),
                IsSelected = !gaming,
            });
        }

        // 4. Startup
        var startupCandidates = startup.Where(s => s.IsEnabled && s.IsKnownNonEssential && !s.IsProtected).ToList();
        if (startupCandidates.Count > 0)
        {
            findings.Add(new ThermalFinding
            {
                Priority = 4,
                Category = "Startup programs",
                Problem = $"{startupCandidates.Count} non-essential app(s) start with Windows",
                Current = string.Join(", ", startupCandidates.Select(s => s.Name).Take(5)) + (startupCandidates.Count > 5 ? "…" : string.Empty),
                Recommended = "Disable autostart (apps still work when opened manually)",
                Impact = ImpactLevel.Low,
                ImpactNote = "Less background load after every boot – may reduce idle CPU load and temperature.",
                IsReversible = true,
                Apply = async backup =>
                {
                    int ok = 0;
                    foreach (var entry in startupCandidates)
                    {
                        if ((await StartupService.SetEnabledAsync(entry, false, backup)).Success)
                        {
                            ok++;
                        }
                    }

                    return ok == startupCandidates.Count ? OperationResult.Ok($"{ok} startup app(s) disabled") : OperationResult.Fail($"{ok} of {startupCandidates.Count} disabled");
                },
                IsSelected = true,
            });
        }

        // 5. Overlays
        foreach (var u in usages.Where(u => OverlayProcesses.Contains(u.Name)))
        {
            var group = ToGroup(u);
            findings.Add(new ThermalFinding
            {
                Priority = 5,
                Category = "Overlay",
                Problem = $"Overlay running: {u.Name}",
                Current = $"CPU {u.CpuText} · RAM {u.RamText}",
                Recommended = "Close it if you don't use it",
                Impact = u.Cpu >= 3 ? ImpactLevel.Medium : ImpactLevel.Low,
                Apply = group is null ? null : _ => CloseAsync(group, allowKill: true),
                IsSelected = !MonitoringOverlays.Contains(u.Name),
            });
        }

        var recording = new BackgroundRecordingOptimization();
        recording.Refresh(context);
        if (recording.IsAvailable && recording.NeedsChange)
        {
            findings.Add(FromOptimization(recording, 5, "Xbox Game Bar", ImpactLevel.Medium, true, context,
                "Continuous background recording encodes video all the time – turning it off may reduce GPU/CPU load."));
        }

        var dvr = new GameDvrOptimization();
        dvr.Refresh(context);
        if (dvr.IsAvailable && GameFeatures.GameDvrEnabled)
        {
            findings.Add(FromOptimization(dvr, 5, "Xbox Game Bar", ImpactLevel.Low, context.Profile.IsLowPowerIntegrated, context, null));
        }

        // 6. Cooling advice
        if (m.AverageTemperature >= 75)
        {
            findings.Add(new ThermalFinding
            {
                Priority = 6,
                Category = "Cooling",
                Problem = "Check cooling conditions",
                Current = $"{m.AverageTemperature:0} °C average during the scan",
                Recommended = "Hard, flat surface · vents free · no bed/blanket · consider room temperature · clean vents carefully if needed",
                Impact = (m.AverageCpuLoad ?? 100) < 25 ? ImpactLevel.High : ImpactLevel.Medium,
                ImpactNote = "Better airflow lets the fan remove heat more effectively. Software cannot do this part.",
            });
        }

        // 7. Optional Windows tweaks (only on low-power iGPU systems)
        if (context.Profile.IsLowPowerIntegrated)
        {
            foreach (var opt in new Optimization[] { new TransparencyOptimization(), new AnimationsOptimization() })
            {
                opt.Refresh(context);
                if (opt.IsAvailable && opt.NeedsChange)
                {
                    findings.Add(FromOptimization(opt, 7, "Optional Windows setting", ImpactLevel.Low, false, context,
                        "Tiny effect – less desktop GPU work on integrated graphics."));
                }
            }
        }

        return findings.OrderBy(f => f.Priority).ThenByDescending(f => f.Impact).ToList();
    }

    private static ThermalFinding FromOptimization(Optimization opt, int priority, string category, ImpactLevel impact, bool select, OptimizationContext context, string? note) => new()
    {
        Priority = priority,
        Category = category,
        Problem = opt.Name,
        Current = opt.CurrentState,
        Recommended = opt.RecommendedState,
        Impact = impact,
        ImpactNote = note ?? "May reduce background load and therefore may reduce temperature.",
        Tradeoff = opt.Downsides,
        IsReversible = opt.IsReversible,
        Apply = backup => opt.ApplyAsync(backup, context),
        IsSelected = select,
    };

    private static AppGroup? ToGroup(ProcessUsage u)
    {
        var pids = new List<int>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName(u.Name))
        {
            using (p)
            {
                pids.Add(p.Id);
            }
        }

        return pids.Count == 0 ? null : new AppGroup
        {
            ProcessName = u.Name,
            DisplayName = u.Name,
            ProcessIds = pids,
            RamBytes = u.Ram,
            IsKnownBackgroundApp = BackgroundAppsService.KnownBackgroundApps.Contains(u.Name),
        };
    }

    private static async Task<OperationResult> CloseAsync(AppGroup group, bool allowKill = false)
    {
        if (allowKill)
        {
            int closed = 0;
            foreach (int pid in group.ProcessIds)
            {
                if ((await ProcessService.CloseAsync(pid, group.DisplayName, true)).Success)
                {
                    closed++;
                }
            }

            return closed > 0 ? OperationResult.Ok($"{group.DisplayName} closed") : OperationResult.Fail($"{group.DisplayName} could not be closed");
        }

        var (ok, message) = await BackgroundAppsService.CloseGroupAsync(group);
        return ok ? OperationResult.Ok(message) : OperationResult.Fail(message);
    }

    private static OperationResult SetBalanced(BackupSession backup, Guid scheme, Guid? overlay)
    {
        backup.CapturePowerScheme();
        if (scheme == PowerManager.Balanced && overlay != null)
        {
            backup.CapturePowerOverlay();
            PowerManager.SetOverlay(PowerManager.OverlayBalanced);
            ActionLog.Success("Thermal: power mode", PowerManager.OverlayName(overlay), "Balanced");
        }
        else
        {
            PowerManager.SetActiveScheme(PowerManager.Balanced);
            ActionLog.Success("Thermal: power plan", PowerManager.GetFriendlyName(scheme), "Balanced");
        }

        return OperationResult.Ok("Balanced power mode active");
    }

    /// <summary>
    /// Thermal Health 0–100 from measured values only. A guide value – not a technical or safety guarantee.
    /// </summary>
    public static (int score, bool partial, string summary) Score(ThermalMeasurement m, double bgCpu, bool aggressive)
    {
        double load = m.AverageCpuLoad ?? 0;
        double bgScore = bgCpu < 3 ? 100 : bgCpu < 10 ? 65 : bgCpu < 20 ? 40 : 20;
        double powerScore = aggressive ? 70 : 100;
        if (m.AverageTemperature is not double t)
        {
            int partialScore = (int)Math.Round(0.7 * bgScore + 0.3 * powerScore);
            return (partialScore, true, "Thermal sensor unavailable – this score only reflects background load and power settings.");
        }

        double tempScore = Math.Clamp((98 - t) / (98 - 60) * 100, 0, 100);
        double loadContext = t >= 75 && load < 25 ? 30 : t >= 75 && load >= 50 ? 85 : 100;
        int score = (int)Math.Round(0.45 * tempScore + 0.2 * loadContext + 0.25 * bgScore + 0.1 * powerScore);

        string summary = t >= 75 && load >= 50 ? "Temperature is elevated, but current CPU load is also high."
            : t >= 75 && load < 25 ? "Temperature is high while CPU load is low. Software optimization may have limited impact. Check ventilation and cooling."
            : t >= 75 ? "Temperature is elevated at moderate CPU load."
            : "Temperature is within the normal range for the current load.";
        return (score, false, summary);
    }

    private static List<string> BuildRecommendations(ThermalMeasurement m, List<ProcessUsage> usages, double bgCpu, bool aggressive, string? game)
    {
        var list = new List<string>();
        if (m.AverageCpuLoad is double load)
        {
            list.Add($"Your CPU is currently using {load:0} % load.");
        }

        foreach (var u in usages.Where(u => !u.IsSystem && u.Cpu >= 3).OrderByDescending(u => u.Cpu).Take(3))
        {
            list.Add($"{u.Name} is using {u.Cpu:0} % CPU.");
        }

        if (bgCpu >= 3)
        {
            list.Add("Closing unnecessary background applications may reduce CPU workload.");
        }

        if (game != null)
        {
            list.Add("A game is running – higher temperatures are expected under gaming load. An FPS cap (e.g. at your display's refresh rate) is one of the most effective ways to reduce gaming heat.");
        }

        if (aggressive)
        {
            list.Add("'Best performance' power mode produces more heat. 'Balanced' usually runs cooler with a small performance cost.");
        }

        if (m.AverageTemperature >= 75 && (m.AverageCpuLoad ?? 100) < 25)
        {
            list.Add("Temperature is high while CPU load is low.");
            list.Add("Software optimization may have limited impact. Check ventilation and cooling.");
        }

        if (m.AverageTemperature is null)
        {
            list.Add("No temperature sensor is readable. For exact CPU temperatures start HWiNFO (Shared Memory enabled) or LibreHardwareMonitor in the background – ConstiBoost reads their values read-only.");
        }

        return list;
    }
}
