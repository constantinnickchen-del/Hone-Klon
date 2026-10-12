using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.FpsBoost;

/// <summary>A Game Focus behaviour that can be switched on/off (applied only while Focus runs, restored on Stop).</summary>
public sealed class FocusToggleFeature : FpsBoostFeature
{
    private readonly Func<AppSettings, bool> _get;
    private readonly Action<AppSettings, bool> _set;
    private readonly Func<string?>? _availability;

    public FocusToggleFeature(string id, string name, string icon, string impact, string description, string downside,
        Func<AppSettings, bool> get, Action<AppSettings, bool> set, Func<string?>? availability = null)
    {
        Id = id;
        Name = name;
        Icon = icon;
        Impact = impact;
        Description = description;
        Downside = downside;
        _get = get;
        _set = set;
        _availability = availability;
    }

    public override string Id { get; }
    public override string Name { get; }
    public override string Icon { get; }
    public override string Category => "Game Focus";
    public override string Impact { get; }
    public override string Description { get; }
    public override string Downside { get; }
    public override bool ChangesSettings => false;

    protected override void CheckCore()
    {
        string? reason = _availability?.Invoke();
        if (reason != null)
        {
            Unavailable(reason);
            Actions = Array.Empty<BoostAction>();
            return;
        }

        bool on = _get(SettingsService.Instance.Current);
        Set(on ? CheckStatus.Good : CheckStatus.Warning, on ? "ON during Game Focus" : "OFF",
            on ? "Applied automatically when you start 🎯 Focus in Games – restored when you stop it." : "Turn on to use it in every Game Focus.", false);
        Actions = new[]
        {
            new BoostAction(on ? "Turn off" : "Turn on", new RelayCommand(() =>
            {
                _set(SettingsService.Instance.Current, !on);
                SettingsService.Instance.Save();
                Check();
            })),
        };
    }
}

/// <summary>Running overlays / recorders that cost FPS in every game.</summary>
public sealed class OverlayDetectorFeature : FpsBoostFeature
{
    private static readonly (string Process, string Name, string Tip)[] Known =
    {
        ("Discord", "Discord", "Discord → Settings → Game Overlay OFF (and 'Hardware acceleration' OFF in Advanced if the game stutters)."),
        ("obs64", "OBS Studio", "Close OBS when you are not recording – its capture costs GPU time."),
        ("Overwolf", "Overwolf", "Overwolf apps hook into games – close it while playing."),
        ("Medal", "Medal.tv", "Medal records in the background – close it or turn off its clip recording."),
        ("MSIAfterburner", "MSI Afterburner", "Close it if you only use it for the overlay."),
        ("RTSS", "RivaTuner Statistics Server", "Its overlay/limiter can lower FPS – close it if you do not need it."),
        ("GameBar", "Xbox Game Bar", "Win+G overlay – closes itself, but background recording can be turned off in Optimizer."),
        ("Outplayed", "Outplayed", "Background clip recorder – close it while playing."),
        ("NVIDIA Share", "NVIDIA overlay", "GeForce Experience → In-game overlay OFF."),
        ("Spotify", "Spotify", "Fine for music, but its animated UI costs a bit – minimize it."),
        ("Wallpaper64", "Wallpaper Engine", "Animated wallpapers use the GPU – pause it while gaming."),
        ("lively", "Lively Wallpaper", "Animated wallpapers use the GPU – pause it while gaming."),
    };

    private List<(int Pid, string Name)> _found = new();

    public override string Id => "overlays";
    public override string Name => "Overlay & recorder detector";
    public override string Icon => "🎥";
    public override string Category => "Network & Background";
    public override string Impact => "MEDIUM";
    public override string Description => "Finds running overlays, recorders and animated wallpapers that cost FPS or cause stutter in every game (Discord overlay, OBS, Overwolf, Medal, Afterburner/RTSS, Wallpaper Engine …).";
    public override string Downside => "Closing apps only like clicking X – nothing is force-killed.";
    public override bool ChangesSettings => false;

    protected override void CheckCore()
    {
        _found = new List<(int, string)>();
        var tips = new List<string>();
        foreach (var (process, name, tip) in Known)
        {
            var procs = Process.GetProcessesByName(process);
            if (procs.Length > 0)
            {
                _found.AddRange(procs.Select(p => (p.Id, name)));
                tips.Add($"• {name}: {tip}");
            }

            foreach (var p in procs)
            {
                p.Dispose();
            }
        }

        Set(tips.Count == 0 ? CheckStatus.Good : CheckStatus.Warning,
            tips.Count == 0 ? "Nothing found" : $"{tips.Count} found",
            tips.Count == 0 ? "No known overlay or recorder is running." : string.Join("\n", tips), false);
        Actions = new List<BoostAction>
        {
            new("Check again", new AsyncRelayCommand(() => Task.Run(Check))),
        }.Concat(tips.Count == 0 ? Array.Empty<BoostAction>() : new[] { new BoostAction("Close all found", new AsyncRelayCommand(CloseAllAsync)) }).ToList();
    }

    private async Task CloseAllAsync()
    {
        var names = _found.Select(f => f.Name).Distinct().ToList();
        if (!DialogService.Confirm("Close overlays & recorders?", "These apps are closed normally (like clicking X):", names, "CLOSE"))
        {
            return;
        }

        int closed = 0;
        foreach (var (pid, name) in _found)
        {
            if ((await ProcessService.CloseAsync(pid, name, false)).Success)
            {
                closed++;
            }
        }

        ResultText = $"✓ Closed {closed} process(es). Apps without a window (tray only) have to be closed from their tray icon.";
        await Task.Run(Check);
    }
}

/// <summary>What is eating CPU right now (besides the game)?</summary>
public sealed class CpuHogFeature : FpsBoostFeature
{
    private static readonly Dictionary<string, string> Explanations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MsMpEng"] = "Microsoft Defender is scanning. ConstiBoost never disables Defender – wait until it finishes or play a bit later.",
        ["TiWorker"] = "Windows is installing updates – let it finish (Settings → Windows Update shows the progress).",
        ["MoUsoCoreWorker"] = "Windows Update is working – let it finish or pause updates for a few days in Settings.",
        ["TrustedInstaller"] = "Windows is installing updates/components – let it finish.",
        ["SearchIndexer"] = "Windows Search is indexing files – Game Focus can pause it (feature 'Pause search indexing').",
        ["OneDrive"] = "OneDrive is syncing – pause syncing from its tray icon while playing.",
        ["steamwebhelper"] = "Steam's browser UI – minimize Steam to the tray.",
        ["EpicGamesLauncher"] = "Epic Launcher – downloads/updates in the background; close it if you do not play an Epic game.",
        ["CompPkgSrv"] = "Windows component service – usually short.",
    };

    public override string Id => "cpu-hogs";
    public override string Name => "Background CPU hog finder";
    public override string Icon => "🐷";
    public override string Category => "Network & Background";
    public override string Impact => "MEDIUM";
    public override string Description => "Measures for 2 seconds which programs use the most CPU right now and explains each (Defender scan, Windows Update, indexing, sync…). Start it while the game is running to see who competes with your game.";
    public override bool ChangesSettings => false;

    protected override void CheckCore()
    {
        var first = Snapshot();
        System.Threading.Thread.Sleep(2000);
        var second = Snapshot();
        int cores = Environment.ProcessorCount;
        var usage = second.Where(kv => first.ContainsKey(kv.Key))
            .Select(kv => (kv.Value.Name, Pct: (kv.Value.Cpu - first[kv.Key].Cpu).TotalMilliseconds / 2000.0 / cores * 100))
            .Where(u => u.Pct >= 2 && u.Name != "Idle" && u.Name != "System" && !u.Name.Equals("ConstiBoost", StringComparison.OrdinalIgnoreCase))
            .GroupBy(u => u.Name).Select(g => (Name: g.Key, Pct: g.Sum(x => x.Pct))).OrderByDescending(u => u.Pct).Take(6).ToList();
        var lines = usage.Select(u => $"• {u.Name}: {u.Pct:0} % CPU" + (Explanations.TryGetValue(u.Name, out string? e) ? " – " + e : string.Empty)).ToList();
        bool heavy = usage.Any(u => Explanations.ContainsKey(u.Name) && u.Pct >= 5);
        Set(heavy ? CheckStatus.Warning : CheckStatus.Good,
            usage.Count == 0 ? "Quiet" : $"Top: {usage[0].Name} {usage[0].Pct:0} %",
            usage.Count == 0 ? "No program uses noticeable CPU right now." : string.Join("\n", lines), false);
        Actions = new[] { new BoostAction("Measure again (2 s)", new AsyncRelayCommand(() => Task.Run(Check))) };
    }

    private static Dictionary<int, (string Name, TimeSpan Cpu)> Snapshot()
    {
        var map = new Dictionary<int, (string, TimeSpan)>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    map[p.Id] = (p.ProcessName, p.TotalProcessorTime);
                }
                catch
                {
                    // protected system process
                }
            }
        }

        return map;
    }
}

/// <summary>AMD Software (Adrenalin) presence, driver age and the settings that matter.</summary>
public sealed class AmdAdrenalinFeature : FpsBoostFeature
{
    private const string DriverPage = "https://www.amd.com/en/support/download/drivers.html";

    public override string Id => "amd";
    public override string Name => "AMD Adrenalin checklist";
    public override string Icon => "🔴";
    public override string Category => "Drivers & Hardware";
    public override string Impact => "HIGH";
    public override string Description => "Current AMD drivers are faster and browsers only accelerate WebGL on drivers they trust. In AMD Software (Gaming → Graphics) these settings give the most FPS: Radeon Chill OFF · Wait for Vertical Refresh 'Always off' · Texture Filtering Quality 'Performance' · Tessellation 'AMD optimized' · Radeon Anti-Lag ON (input lag) · Radeon Super Resolution for fullscreen games. ConstiBoost does not change driver settings – they belong to AMD Software.";
    public override bool ChangesSettings => false;

    private static string? AdrenalinPath()
    {
        string[] candidates =
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\AMD\CNext\CNext\RadeonSoftware.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\AMD\CNext\CNext\AMDRSServ.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    protected override void CheckCore()
    {
        var gpu = ScanService.Instance.Last?.Hardware.Gpus.FirstOrDefault(g => g.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || g.Name.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
        if (gpu is null)
        {
            Unavailable("No AMD graphics detected – this checklist is for AMD Radeon graphics.");
            Actions = Array.Empty<BoostAction>();
            return;
        }

        string? path = AdrenalinPath();
        var notes = new List<string>();
        bool old = gpu.DriverDate is DateTime d && d < DateTime.Now.AddMonths(-12);
        notes.Add(gpu.DriverDate is DateTime date ? $"Driver {gpu.DriverVersion} from {date:MMMM yyyy}" + (old ? " – older than 1 year, update recommended." : " – recent.") : $"Driver {gpu.DriverVersion}");
        notes.Add(path != null ? "AMD Software (Adrenalin) is installed." : "AMD Software (Adrenalin) not found – install it from amd.com (it includes the driver).");
        Set(old || path is null ? CheckStatus.Warning : CheckStatus.Good, old ? "Driver outdated" : path is null ? "AMD Software missing" : "Driver OK", string.Join("\n", notes), false);
        var actions = new List<BoostAction> { new("Open AMD driver download", new RelayCommand(() => Open(DriverPage))) };
        if (path != null)
        {
            actions.Insert(0, new BoostAction("Open AMD Software", new RelayCommand(() => Open(path))));
        }

        Actions = actions;
    }

    private static void Open(string target) =>
        Safe.Try(() => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose(), "Open " + target);
}

/// <summary>Single- vs dual-channel memory – the biggest hardware factor for integrated graphics.</summary>
public sealed class DualChannelFeature : FpsBoostFeature
{
    public override string Id => "dual-channel";
    public override string Name => "RAM dual-channel check";
    public override string Icon => "🧩";
    public override string Category => "Drivers & Hardware";
    public override string Impact => "HIGH";
    public override string Description => "Integrated Radeon graphics use your RAM as video memory. With only ONE memory module the GPU gets half the bandwidth – a second matching module typically gives +30-50 % FPS in games. No setting can replace this.";
    public override bool ChangesSettings => false;

    protected override void CheckCore()
    {
        var layout = MemoryInfo.Read();
        if (layout.Modules.Count == 0)
        {
            Unavailable("Memory modules could not be read on this PC.");
            return;
        }

        var m = layout.Modules[0];
        string modules = string.Join(" + ", layout.Modules.Select(x => $"{x.CapacityBytes / 1024 / 1024 / 1024} GB"));
        if (layout.Modules.Count >= 2)
        {
            Set(CheckStatus.Good, $"Dual-channel ({modules})", "Optimal – both memory channels are used.", false);
            return;
        }

        string slot = layout.FreeSlots is > 0 ? "A free slot was detected – you can add a module." : layout.Slots is 1 ? "Windows reports only one slot: the second module may be soldered or not upgradeable – check the laptop manual." : "Check the laptop manual for a free slot.";
        Set(CheckStatus.Warning, $"Single-channel ({modules})",
            $"{slot}\nRecommended: 1 × {m.CapacityBytes / 1024 / 1024 / 1024} GB {m.Type} SO-DIMM {(m.SpeedMts is int s ? s + " MT/s" : string.Empty)} (same size and speed as the existing one).", false);
    }
}
