using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed class ScanResult
{
    public HardwareInfo Hardware { get; init; } = new();
    public SystemState State { get; init; } = new();
    public DeviceProfile Profile { get; init; } = DeviceProfile.From(new HardwareInfo());
    public List<ReadinessCheck> Checks { get; init; } = new();
    public int Score { get; init; }
    public PerformanceStatus Status { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

/// <summary>Full PC scan: hardware + gaming-relevant Windows state + Gaming Readiness score.</summary>
public sealed class ScanService
{
    public static ScanService Instance { get; } = new();

    public ScanResult? Last { get; private set; }

    public event Action<ScanResult>? Completed;

    public async Task<ScanResult> ScanAsync(IProgress<string>? progress = null)
    {
        var result = await Task.Run(() =>
        {
            progress?.Report("Detecting CPU, GPU and memory…");
            var hw = HardwareScanner.Scan();

            progress?.Report("Reading Windows gaming settings…");
            var state = ReadState(hw);

            progress?.Report("Checking startup apps…");
            var startup = Core.Safe.Try(StartupService.GetEntries, new List<StartupEntry>(), "Startup scan");
            state.TotalStartupApps = startup.Count;
            state.EnabledStartupApps = startup.Count(s => s.IsEnabled && !s.IsProtected);

            progress?.Report("Measuring background load…");
            state.IdleCpuPercent = MeasureCpu();

            var profile = DeviceProfile.From(hw);
            var checks = BuildChecks(hw, state, profile);
            int score = checks.Sum(c => c.Points);
            return new ScanResult
            {
                Hardware = hw,
                State = state,
                Profile = profile,
                Checks = checks,
                Score = score,
                Status = score >= 80 ? PerformanceStatus.Good : score >= 55 ? PerformanceStatus.CouldBeImproved : PerformanceStatus.NeedsAttention,
                Timestamp = DateTime.Now,
            };
        });

        Last = result;
        AppLog.Info($"Scan completed: score {result.Score}, CPU '{result.Hardware.CpuName}', GPU '{result.Hardware.PrimaryGpuName}'");
        Completed?.Invoke(result);
        return result;
    }

    private static double? MeasureCpu()
    {
        var sampler = new CpuUsageSampler();
        sampler.Sample();
        var values = new List<double>();
        for (int i = 0; i < 4; i++)
        {
            Thread.Sleep(500);
            if (sampler.Sample() is double v)
            {
                values.Add(v);
            }
        }

        return values.Count == 0 ? null : values.Average();
    }

    private static SystemState ReadState(HardwareInfo hw)
    {
        var state = new SystemState
        {
            GameModeEnabled = Core.Safe.Try<bool?>(() => GameFeatures.GameModeEnabled, null),
            GameBarEnabled = Core.Safe.Try<bool?>(() => GameFeatures.GameBarControllerEnabled, null),
            GameDvrEnabled = Core.Safe.Try<bool?>(() => GameFeatures.GameDvrEnabled, null),
            BackgroundRecordingEnabled = Core.Safe.Try<bool?>(() => GameFeatures.BackgroundRecordingEnabled, null),
            HagsEnabled = GameFeatures.HagsEnabled,
            TransparencyEnabled = Core.Safe.Try<bool?>(() => GameFeatures.TransparencyEnabled, null),
        };
        state.HagsText = state.HagsEnabled switch { true => "ON", false => "OFF", _ => "Unavailable" };

        var scheme = PowerManager.GetActiveSchemeId();
        state.PowerPlanName = scheme.HasValue ? PowerManager.GetFriendlyName(scheme.Value) : Core.Formatting.Unavailable;
        state.PowerModeName = scheme == PowerManager.Balanced ? PowerManager.OverlayName(PowerManager.GetOverlay()) : "–";

        var memory = SystemInfo.GetMemoryStatus();
        if (memory.HasValue)
        {
            state.RamUsedPercent = memory.Value.loadPercent;
            state.RamAvailableBytes = (long)memory.Value.available;
        }

        var sys = hw.Drives.FirstOrDefault(d => d.IsSystemDrive);
        if (sys != null && sys.TotalBytes > 0)
        {
            state.SystemDriveFreePercent = 100.0 * sys.FreeBytes / sys.TotalBytes;
        }

        return state;
    }

    private static List<ReadinessCheck> BuildChecks(HardwareInfo hw, SystemState s, DeviceProfile profile)
    {
        var checks = new List<ReadinessCheck>();

        checks.Add(s.GameModeEnabled switch
        {
            true => new ReadinessCheck("Game Mode enabled", CheckStatus.Good, 15, 15),
            false => new ReadinessCheck("Game Mode is turned off", CheckStatus.Bad, 0, 15),
            _ => new ReadinessCheck("Game Mode status unavailable", CheckStatus.Unavailable, 8, 15),
        });

        var scheme = PowerManager.GetActiveSchemeId();
        var overlay = scheme == PowerManager.Balanced ? PowerManager.GetOverlay() : null;
        bool perfPower = scheme == PowerManager.HighPerformance || scheme == PowerManager.UltimatePerformance ||
                         overlay == PowerManager.OverlayBestPerformance || overlay == PowerManager.OverlayBetterPerformance;
        bool savingPower = scheme == PowerManager.PowerSaver || overlay == PowerManager.OverlayBestEfficiency;
        if (perfPower)
        {
            checks.Add(new ReadinessCheck("Power profile optimized for performance", CheckStatus.Good, 15, 15));
        }
        else if (savingPower)
        {
            checks.Add(new ReadinessCheck("Power saving mode limits CPU/GPU clocks", CheckStatus.Bad, 0, 15));
        }
        else
        {
            checks.Add(new ReadinessCheck("Power mode is balanced – could be improved", CheckStatus.Warning, 8, 15));
        }

        if (hw.HasBattery && hw.OnAcPower == false)
        {
            checks.Add(new ReadinessCheck("Running on battery – plug in for full performance", CheckStatus.Warning, 0, 0));
        }

        checks.Add(s.EnabledStartupApps switch
        {
            <= 4 => new ReadinessCheck($"Startup apps under control ({s.EnabledStartupApps} active)", CheckStatus.Good, 15, 15),
            <= 8 => new ReadinessCheck($"Some startup apps active ({s.EnabledStartupApps})", CheckStatus.Warning, 8, 15),
            _ => new ReadinessCheck($"Many startup apps active ({s.EnabledStartupApps})", CheckStatus.Bad, 0, 15),
        });

        checks.Add(s.IdleCpuPercent switch
        {
            null => new ReadinessCheck("Background load unavailable", CheckStatus.Unavailable, 8, 15),
            < 10 => new ReadinessCheck($"Background processes normal ({s.IdleCpuPercent:0}% CPU)", CheckStatus.Good, 15, 15),
            < 25 => new ReadinessCheck($"Elevated background load ({s.IdleCpuPercent:0}% CPU)", CheckStatus.Warning, 8, 15),
            _ => new ReadinessCheck($"High background load ({s.IdleCpuPercent:0}% CPU)", CheckStatus.Bad, 0, 15),
        });

        checks.Add(s.RamUsedPercent switch
        {
            null => new ReadinessCheck("RAM usage unavailable", CheckStatus.Unavailable, 8, 15),
            < 70 => new ReadinessCheck($"RAM usage OK ({s.RamUsedPercent:0}%)", CheckStatus.Good, 15, 15),
            < 85 => new ReadinessCheck($"RAM usage high ({s.RamUsedPercent:0}%)", CheckStatus.Warning, 8, 15),
            _ => new ReadinessCheck($"RAM almost full ({s.RamUsedPercent:0}%)", CheckStatus.Bad, 0, 15),
        });

        checks.Add(s.SystemDriveFreePercent switch
        {
            null => new ReadinessCheck("System drive space unavailable", CheckStatus.Unavailable, 5, 10),
            >= 15 => new ReadinessCheck($"Enough free space on system drive ({s.SystemDriveFreePercent:0}%)", CheckStatus.Good, 10, 10),
            >= 8 => new ReadinessCheck($"System drive getting full ({s.SystemDriveFreePercent:0}% free)", CheckStatus.Warning, 5, 10),
            _ => new ReadinessCheck($"System drive almost full ({s.SystemDriveFreePercent:0}% free)", CheckStatus.Bad, 0, 10),
        });

        checks.Add(s.BackgroundRecordingEnabled switch
        {
            false => new ReadinessCheck("Background recording off", CheckStatus.Good, 10, 10),
            true => new ReadinessCheck("Xbox background recording is active", CheckStatus.Warning, 0, 10),
            _ => new ReadinessCheck("Background recording status unavailable", CheckStatus.Unavailable, 5, 10),
        });

        if (profile.IsLowPowerIntegrated)
        {
            checks.Add(s.TransparencyEnabled == true
                ? new ReadinessCheck("Transparency effects active (costs iGPU time)", CheckStatus.Warning, 0, 5)
                : new ReadinessCheck("Visual effects lightweight", CheckStatus.Good, 5, 5));
        }
        else
        {
            checks.Add(new ReadinessCheck("Visual effects OK for this GPU", CheckStatus.Good, 5, 5));
        }

        return checks;
    }
}
