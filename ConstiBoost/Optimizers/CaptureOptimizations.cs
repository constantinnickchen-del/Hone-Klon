using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

public sealed class BackgroundRecordingOptimization : Optimization
{
    public override string Id => "bgrecording";
    public override string Name => L.T("Xbox background recording");
    public override string Category => L.T("Gaming");
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;

    public override string WhatItDoes =>
        L.T("'Record what happened' keeps continuously encoding the last minutes of gameplay in the background. Turning it off frees GPU encoder, CPU and disk time.");

    public override string Downsides => L.T("You can no longer save the last 30 seconds as a clip after the fact. Manual recording (Win+Alt+R) still works.");

    protected override void RefreshCore(OptimizationContext context)
    {
        bool on = GameFeatures.BackgroundRecordingEnabled;
        CurrentState = OnOff(on);
        RecommendedState = "OFF";
        Reason = L.T("Continuous background recording costs performance on every game, especially on integrated graphics.");
        NeedsChange = on;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context) =>
        Task.FromResult(SetUserDword(backup, GameFeatures.GameDvrKey, GameFeatures.HistoricalCaptureValue, 0, CurrentState, "OFF"));
}

public sealed class GameDvrOptimization : Optimization
{
    public override string Id => "gamedvr";
    public override string Name => L.T("Xbox Game Bar captures (Game DVR)");
    public override string Category => L.T("Gaming");
    public override string Icon => "";

    public override string WhatItDoes =>
        L.T("Disables the Game Bar capture feature (screenshots/recordings via Game Bar). The Game Bar overlay itself (Win+G widgets) stays available.");

    public override string Downsides => L.T("Game Bar recording and screenshots stop working until you turn captures back on (Settings > Gaming > Captures).");

    public override bool PreselectWhenNeeded(OptimizationContext context) => context.Profile.IsLowPowerIntegrated;

    protected override void RefreshCore(OptimizationContext context)
    {
        bool on = GameFeatures.GameDvrEnabled;
        CurrentState = OnOff(on);
        RecommendedState = context.Profile.IsLowPowerIntegrated ? "OFF" : "Your choice";
        Reason = context.Profile.IsLowPowerIntegrated
            ? L.T("On low-power integrated graphics the capture hooks add overhead you probably do not need.")
            : L.T("Only matters if you do not use Game Bar recordings.");
        NeedsChange = on && context.Profile.IsLowPowerIntegrated;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        string old = CurrentState;
        var first = SetUserDword(backup, GameFeatures.GameDvrKey, GameFeatures.AppCaptureValue, 0, old, "OFF", "Game DVR: AppCaptureEnabled");
        if (!first.Success)
        {
            return Task.FromResult(first);
        }

        var second = SetUserDword(backup, GameFeatures.GameConfigStoreKey, GameFeatures.GameDvrEnabledValue, 0, old, "OFF", "Game DVR: GameDVR_Enabled");
        return Task.FromResult(second.Success ? OperationResult.Ok($"{old} → {L.T("OFF")}") : second);
    }
}

public sealed class TransparencyOptimization : Optimization
{
    public override string Id => "transparency";
    public override string Name => L.T("Transparency effects");
    public override string Category => L.T("Visual");
    public override string Icon => "";

    public override string WhatItDoes => L.T("Turns off the blur/transparency of the taskbar, Start menu and windows (Settings > Personalization > Colors).");

    public override string Downsides => L.T("Windows looks a bit flatter. No functional change.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => context.Profile.IsLowPowerIntegrated;

    protected override void RefreshCore(OptimizationContext context)
    {
        bool on = GameFeatures.TransparencyEnabled;
        CurrentState = OnOff(on);
        RecommendedState = context.Profile.IsLowPowerIntegrated ? "OFF" : "Your choice";
        Reason = context.Profile.IsLowPowerIntegrated
            ? L.T("Integrated GPUs share power and memory with the CPU – desktop blur effects take a small share of that.")
            : L.T("Negligible effect on dedicated GPUs.");
        NeedsChange = on && context.Profile.IsLowPowerIntegrated;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        var result = SetUserDword(backup, GameFeatures.PersonalizeKey, GameFeatures.TransparencyValue, 0, CurrentState, "OFF");
        WinSys.ShellNotify.BroadcastSettingChange("ImmersiveColorSet");
        return Task.FromResult(result);
    }
}

public sealed class BackgroundAppsOptimization : Optimization
{
    public override string Id => "bgapps";
    public override string Name => L.T("Background apps (Store apps)");
    public override string Category => L.T("Background");
    public override string Icon => "";

    public override string WhatItDoes =>
        L.T("Stops Microsoft Store apps from running in the background (global 'Let apps run in background' switch). Desktop programs are not affected.");

    public override string Downsides =>
        L.T("Store apps (e.g. Mail, Calendar, Phone Link) may not sync or show notifications until opened. On some Windows 11 builds only per-app settings apply, so the effect can be small.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => false;

    protected override void RefreshCore(OptimizationContext context)
    {
        bool disabled = GameFeatures.BackgroundAppsDisabled;
        CurrentState = disabled ? "Blocked" : "Allowed";
        RecommendedState = "Optional: Blocked";
        Reason = L.T("Fewer background tasks while gaming. Optional because it can delay notifications.");
        NeedsChange = !disabled;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context) =>
        Task.FromResult(SetUserDword(backup, GameFeatures.BackgroundAppsKey, GameFeatures.BackgroundAppsValue, 1, CurrentState, "Blocked"));
}

public sealed class HagsOptimization : Optimization
{
    private bool? _enabled;

    public override string Id => "hags";
    public override string Name => L.T("Hardware-accelerated GPU scheduling");
    public override string Category => L.T("Gaming");
    public override string Icon => "";
    public override bool RequiresAdmin => true;
    public override bool IsUserChoice => true;

    public override string WhatItDoes =>
        L.T("Lets the graphics card manage its own memory scheduling instead of Windows. Can reduce latency slightly. Toggling requires administrator permission and a restart.");

    public override string Downsides =>
        L.T("Results depend on the driver: some systems get more stutter with it on, others with it off. Needs a restart to take effect.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => false;

    protected override void RefreshCore(OptimizationContext context)
    {
        _enabled = GameFeatures.HagsEnabled;
        if (_enabled is null)
        {
            IsAvailable = false;
            UnavailableReason = L.T("This optimization is unavailable on your system. Your graphics driver does not expose hardware-accelerated GPU scheduling.");
            CurrentState = Formatting.Unavailable;
            RecommendedState = "–";
            NeedsChange = false;
            return;
        }

        CurrentState = OnOff(_enabled.Value);
        RecommendedState = L.F("Toggle to {0} (test it)", L.T(OnOff(!_enabled.Value)));
        Reason = L.T("No universal best value – try both and keep what feels smoother in your games.");
        NeedsChange = false;
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        if (_enabled is null)
        {
            return OperationResult.Unavailable(L.T("Not supported by the driver."));
        }

        string old = OnOff(_enabled.Value);
        string target = OnOff(!_enabled.Value);
        backup.CaptureRegistry("HKLM", GameFeatures.GraphicsDriversKey, GameFeatures.HagsValue, Name);
        var result = await ElevationService.RunAsync(new ElevatedTask
        {
            Operation = ElevatedOperations.RegistrySetDword,
            DwordWrites =
            {
                new DwordWrite { Hive = "HKLM", KeyPath = GameFeatures.GraphicsDriversKey, ValueName = GameFeatures.HagsValue, Value = _enabled.Value ? 1 : 2 },
            },
        });

        if (!result.Success)
        {
            ActionLog.Failed(Name, old, target, result.Message);
            return OperationResult.Fail("This optimization couldn't be applied.", result.Message);
        }

        ActionLog.Success(Name, old, target + " (after restart)");
        return OperationResult.Ok(L.F("{0} → {1}. Restart Windows to activate.", L.T(old), L.T(target)));
    }
}
