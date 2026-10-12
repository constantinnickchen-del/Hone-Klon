using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Games;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.FpsBoost;

/// <summary>Allow-listed power plan setting, plugged-in (AC) value only – battery behaviour stays unchanged.</summary>
public sealed class PowerSettingFeature : FpsBoostFeature
{
    private readonly PowerSettingDef _def;
    private readonly uint _target;
    private readonly Func<uint, bool> _isOptimal;
    private readonly Func<uint, string> _describe;

    public PowerSettingFeature(string id, string name, string icon, string impact, string description, string downside,
        PowerSettingDef def, uint target, Func<uint, bool> isOptimal, Func<uint, string> describe)
    {
        Id = id;
        Name = L.T(name);
        Icon = icon;
        Impact = L.T(impact);
        Description = L.T(description);
        Downside = L.T(downside);
        _def = def;
        _target = target;
        _isOptimal = isOptimal;
        _describe = describe;
    }

    public override string Id { get; }
    public override string Name { get; }
    public override string Icon { get; }
    public override string Category => L.T("Power & CPU");
    public override string Impact { get; }
    public override string Description { get; }
    public override string Downside { get; }

    protected override void CheckCore()
    {
        var scheme = PowerManager.GetActiveSchemeId();
        uint? value = scheme is null ? null : PowerSettings.ReadAc(_def, scheme);
        if (value is null)
        {
            Unavailable(L.T("This optimization is unavailable on your system (this power setting does not exist on this PC / driver)."));
            return;
        }

        bool optimal = _isOptimal(value.Value);
        Set(optimal ? CheckStatus.Good : CheckStatus.Warning,
            L.F("Plugged in: {0}", L.T(_describe(value.Value))),
            optimal ? L.T("Already optimal.") : L.F("Recommended: {0}", L.T(_describe(_target))),
            !optimal);
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        var scheme = PowerManager.GetActiveSchemeId() ?? throw new InvalidOperationException("The active power plan could not be read.");
        backup.CapturePowerSetting(scheme, _def);
        return await PowerTweakService.SetPowerSettingAsync(scheme, _def, _target, "FPS Boost: " + Name);
    }
}

/// <summary>Windows 11 "Optimizations for windowed games" (flip-model swap chain for DX10/11 windowed games).</summary>
public sealed class WindowedOptimizationsFeature : FpsBoostFeature
{
    private const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string Value = "DirectXUserGlobalSettings";
    private const string Flag = "SwapEffectUpgradeEnable=1;";

    public override string Id => "windowed-opt";
    public override string Name => L.T("Optimizations for windowed games");
    public override string Icon => "🪟";
    public override string Category => L.T("Graphics & Display");
    public override string Impact => L.T("MEDIUM");
    public override string Description => L.T("Windows 11 switches older DirectX 10/11 games in windowed / borderless mode to the modern flip presentation model: lower input lag and often a few more FPS – the same switch as Settings → Display → Graphics → 'Optimizations for windowed games'.");
    public override string Downside => L.T("Very rarely an old game shows issues in windowed mode – Undo restores it.");

    protected override void CheckCore()
    {
        if (Environment.OSVersion.Version.Build < 22000)
        {
            Unavailable(L.T("This optimization is unavailable on your system (Windows 11 only)."));
            return;
        }

        string current = RegistryHelper.ReadString("HKCU", Key, Value) ?? string.Empty;
        bool on = current.Contains(Flag, StringComparison.OrdinalIgnoreCase);
        Set(on ? CheckStatus.Good : CheckStatus.Warning, on ? L.T("On") : L.T("Off"), on ? L.T("Already optimal.") : L.T("Recommended: On"), !on);
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        backup.CaptureRegistry("HKCU", Key, Value, "Windowed game optimizations");
        string current = RegistryHelper.ReadString("HKCU", Key, Value) ?? string.Empty;
        string updated = string.Join(";", current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("SwapEffectUpgradeEnable=", StringComparison.OrdinalIgnoreCase))
            .Append("SwapEffectUpgradeEnable=1")) + ";";
        using var root = RegistryHelper.OpenBase("HKCU");
        using var key = root.CreateSubKey(Key, true);
        key.SetValue(Value, updated, Microsoft.Win32.RegistryValueKind.String);
        ActionLog.Success("FPS Boost: windowed game optimizations", current, updated);
        return Task.FromResult(OperationResult.Ok(L.T("Windowed game optimizations ON (applies to games started from now on).")));
    }
}

/// <summary>Per-app "High performance" GPU preference for every installed browser (web games).</summary>
public sealed class BrowserGpuPreferenceFeature : FpsBoostFeature
{
    private const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public override string Id => "browser-gpu";
    public override string Name => L.T("Browsers on the high-performance GPU");
    public override string Icon => "🌐";
    public override string Category => L.T("Graphics & Display");
    public override string Impact => L.T("HIGH");
    public override string Description => L.T("Sets Windows' graphics preference 'High performance' for all installed browsers, so WebGL games never run on the weaker GPU. Matters on laptops with two graphics chips.");
    public override string Downside => L.T("Slightly more power use while the browser is open (dual-GPU laptops only).");

    private static List<string> Browsers() => BrowserGamesScanner.Scan().Select(b => b.MainExecutable).OfType<string>().ToList();

    protected override void CheckCore()
    {
        var browsers = Browsers();
        if (browsers.Count == 0)
        {
            Unavailable(L.T("No browser found."));
            return;
        }

        int gpus = ScanService.Instance.Last?.Hardware.Gpus.Count ?? 1;
        var missing = browsers.Where(b => !(RegistryHelper.ReadString("HKCU", Key, b) ?? string.Empty).Contains("GpuPreference=2", StringComparison.OrdinalIgnoreCase)).ToList();
        if (gpus <= 1)
        {
            Set(CheckStatus.Good, "Not needed", L.T("Your PC has one graphics chip – browsers always use it. Nothing to do."), missing.Count > 0);
            CanImprove = false;
            return;
        }

        Set(missing.Count == 0 ? CheckStatus.Good : CheckStatus.Warning,
            missing.Count == 0 ? "All browsers: High performance" : L.F("{0} browser(s) not set", missing.Count),
            missing.Count == 0 ? L.T("Already optimal.") : string.Join(", ", missing.Select(Path.GetFileName)),
            missing.Count > 0);
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        using var root = RegistryHelper.OpenBase("HKCU");
        using var key = root.CreateSubKey(Key, true);
        int count = 0;
        foreach (string exe in Browsers())
        {
            backup.CaptureRegistry("HKCU", Key, exe, "GPU preference " + Path.GetFileName(exe));
            key.SetValue(exe, "GpuPreference=2;", Microsoft.Win32.RegistryValueKind.String);
            ActionLog.Success("FPS Boost: GPU preference " + Path.GetFileName(exe), null, "High performance");
            count++;
        }

        return Task.FromResult(OperationResult.Ok(L.F("{0} browser(s) set to High performance – restart the browser.", count)));
    }
}

/// <summary>Highest refresh rate the display supports at the current resolution.</summary>
public sealed class RefreshRateFeature : FpsBoostFeature
{
    public override string Id => "refresh";
    public override string Name => L.T("Highest display refresh rate");
    public override string Icon => "🖥️";
    public override string Category => L.T("Graphics & Display");
    public override string Impact => L.T("MEDIUM");
    public override string Description => L.T("Some laptops run the screen below its maximum (e.g. 48 Hz or 60 Hz on a 120/144 Hz panel, or a power-saving mode). The refresh rate caps the FPS you can SEE and VSync-locked games.");
    public override string Downside => L.T("Higher refresh rates use a little more power. Windows asks to keep the mode – without confirmation it reverts after 15 s.");

    private DisplayMode? _best;

    protected override void CheckCore()
    {
        var current = DisplayModes.Current();
        if (current is null)
        {
            Unavailable(L.T("The display mode could not be read."));
            return;
        }

        _best = DisplayModes.All().Where(m => m.Width == current.Width && m.Height == current.Height).OrderByDescending(m => m.RefreshRate).FirstOrDefault();
        bool better = _best != null && _best.RefreshRate > current.RefreshRate;
        Set(better ? CheckStatus.Warning : CheckStatus.Good, $"{current.RefreshRate} Hz",
            better ? L.F("Your display supports {0} Hz at {1}×{2}.", _best!.RefreshRate, current.Width, current.Height) : "Already the highest refresh rate this display offers.", better);
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        if (_best is null)
        {
            return OperationResult.Unavailable(L.T("No better mode."));
        }

        backup.CaptureDisplayMode();
        var previous = DisplayModes.Current()!;
        DisplayModes.Apply(_best, true);
        if (!await DisplayKeepPrompt.AskAsync(L.F("Keep {0} Hz?", _best.RefreshRate)))
        {
            DisplayModes.Apply(previous, true);
            return OperationResult.Fail(L.F("Reverted to {0} Hz.", previous.RefreshRate));
        }

        ActionLog.Success("FPS Boost: refresh rate", previous.ToString(), _best.ToString());
        return OperationResult.Ok(L.F("Display now runs at {0} Hz.", _best.RefreshRate));
    }
}

/// <summary>Temporary lower desktop resolution – fewer pixels for every game and the browser.</summary>
public sealed class GamingResolutionFeature : FpsBoostFeature
{
    private DisplayMode? _target;

    public override string Id => "gaming-res";
    public override string Name => L.T("Gaming resolution (temporary)");
    public override string Icon => "🔻";
    public override string Category => L.T("Graphics & Display");
    public override string Impact => L.T("HIGH");
    public override string Description => L.T("Lowers the Windows resolution while you play, e.g. 1920×1080 → 1600×900 (−31 % pixels) or 1280×720 (−56 %). Every fullscreen browser game and game in borderless mode renders fewer pixels – the strongest FPS lever on integrated graphics when the GPU is the limit. Not saved permanently: a restart, sign-out or Undo returns to normal.");
    public override string Downside => L.T("The picture is slightly less sharp and desktop icons can move. Does not help when the CPU is the limit (see 🔍 ANALYZE in Games).");
    public override bool InApplyAll => false;

    protected override void CheckCore()
    {
        var current = DisplayModes.Current();
        if (current is null)
        {
            Unavailable(L.T("The display mode could not be read."));
            return;
        }

        var all = DisplayModes.All();
        int nativePixels = all.Max(m => m.Width * m.Height);
        double aspect = (double)current.Width / current.Height;
        var candidates = all.Where(m => Math.Abs((double)m.Width / m.Height - aspect) < 0.02 && m.Width * m.Height < nativePixels && m.Width >= 1280)
            .GroupBy(m => (m.Width, m.Height)).Select(g => g.OrderByDescending(m => m.RefreshRate).First()).OrderByDescending(m => m.Width).ToList();
        _target = candidates.FirstOrDefault(m => m.Width * m.Height <= nativePixels * 0.75) ?? candidates.FirstOrDefault();
        bool lowered = current.Width * current.Height < nativePixels;
        Actions = candidates.Take(3).Select(m => new BoostAction(L.F("Use {0}×{1} (−{2} % pixels)", m.Width, m.Height, 100 - m.Width * m.Height * 100 / nativePixels),
            new AsyncRelayCommand(async () => { _target = m; CanImprove = true; await ApplyAsync(); }))).ToList();
        if (candidates.Count == 0)
        {
            Unavailable(L.T("This optimization is unavailable on your system (no smaller resolution with the same aspect ratio)."));
            return;
        }

        Set(lowered ? CheckStatus.Good : CheckStatus.Warning,
            lowered ? L.F("Active: {0}×{1}", current.Width, current.Height) : L.F("Native {0}×{1}", current.Width, current.Height),
            lowered ? "Gaming resolution is active – press UNDO (or restart) to return to the native resolution." : L.F("Suggested for gaming: {0}×{1}.", _target!.Width, _target.Height),
            !lowered);
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        if (_target is null)
        {
            return OperationResult.Unavailable(L.T("No smaller resolution."));
        }

        backup.CaptureDisplayMode();
        var previous = DisplayModes.Current()!;
        DisplayModes.Apply(_target, false);
        if (!await DisplayKeepPrompt.AskAsync(L.F("Keep {0}×{1} for gaming?", _target.Width, _target.Height)))
        {
            DisplayModes.ResetToSaved();
            return OperationResult.Fail(L.F("Reverted to {0}×{1}.", previous.Width, previous.Height));
        }

        ActionLog.Success("FPS Boost: gaming resolution (temporary)", previous.ToString(), _target.ToString());
        return OperationResult.Ok(L.F("{0}×{1} active until Undo / restart.", _target.Width, _target.Height));
    }

    protected override Task OnUndoneAsync()
    {
        DisplayModes.ResetToSaved();
        return Task.CompletedTask;
    }
}

/// <summary>Delivery Optimization: no peer-to-peer uploading of Windows updates to other PCs (updates still work normally).</summary>
public sealed class DeliveryOptimizationFeature : FpsBoostFeature
{
    private const string Key = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
    private const string Value = "DODownloadMode";

    public override string Id => "delivery-opt";
    public override string Name => L.T("Stop update sharing with other PCs");
    public override string Icon => "📡";
    public override string Category => L.T("Network & Background");
    public override string Impact => L.T("LOW");
    public override string Description => L.T("Windows can upload update files to other PCs on the internet in the background (Delivery Optimization). This uses upload bandwidth and causes lag spikes in online and web games. After the change Windows downloads updates directly from Microsoft – Windows Update keeps working normally.");
    public override string Downside => L.T("Updates are no longer shared between your own PCs on the local network. Needs one UAC prompt.");
    public override bool InApplyAll => false;

    protected override void CheckCore()
    {
        int? mode = RegistryHelper.ReadDword("HKLM", Key, Value);
        bool off = mode is 0 or 99 or 100;
        Set(off ? CheckStatus.Good : CheckStatus.Warning,
            mode is null ? "Windows default (sharing allowed)" : L.F("Mode {0}", mode),
            off ? L.T("Already optimal – no peer-to-peer uploads.") : L.T("Recommended: HTTP only (direct download from Microsoft, no uploading)."),
            !off);
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup)
    {
        backup.CaptureRegistry("HKLM", Key, Value, "Delivery Optimization mode");
        var result = await ElevationService.RunAsync(new ElevatedTask
        {
            Operation = ElevatedOperations.RegistrySetDword,
            DwordWrites = new List<DwordWrite> { new() { Hive = "HKLM", KeyPath = Key, ValueName = Value, Value = 0 } },
        });
        if (result.Success)
        {
            ActionLog.Success("FPS Boost: Delivery Optimization", "sharing", "HTTP only");
            return OperationResult.Ok(L.T("Update sharing is off. Windows Update still downloads directly from Microsoft."));
        }

        return OperationResult.Fail("This optimization couldn't be applied.", result.Message);
    }
}
