using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

public sealed class AnimationsOptimization : Optimization
{
    public override string Id => "animations";
    public override string Name => L.T("Window animations");
    public override string Category => L.T("Visual");
    public override string Icon => "";

    public override string WhatItDoes => L.T("Turns off minimize/maximize and in-window animations (same as Settings > Accessibility > Visual effects > Animation effects).");

    public override string Downsides => L.T("Windows open and close instantly without animation. Purely visual.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => context.Profile.IsLowPowerIntegrated;

    protected override void RefreshCore(OptimizationContext context)
    {
        bool? minimize = SystemParametersTweaks.GetMinimizeAnimation();
        bool? client = SystemParametersTweaks.GetClientAreaAnimation();
        if (minimize is null || client is null)
        {
            IsAvailable = false;
            UnavailableReason = "This optimization is unavailable on your system. Animation settings could not be read.";
            NeedsChange = false;
            CurrentState = Formatting.Unavailable;
            return;
        }

        bool on = minimize.Value || client.Value;
        CurrentState = OnOff(on);
        RecommendedState = context.Profile.IsLowPowerIntegrated ? "OFF" : "Your choice";
        Reason = context.Profile.IsLowPowerIntegrated
            ? "Fewer desktop effects = snappier Alt+Tab and less iGPU work on the desktop."
            : "Mostly cosmetic on dedicated GPUs.";
        NeedsChange = on && context.Profile.IsLowPowerIntegrated;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        string old = CurrentState;
        backup.CaptureSystemParameter(SystemParameterBackup.MinimizeAnimation, SystemParameterBackup.Capture(SystemParameterBackup.MinimizeAnimation), "Minimize/maximize animation");
        backup.CaptureSystemParameter(SystemParameterBackup.ClientAreaAnimation, SystemParameterBackup.Capture(SystemParameterBackup.ClientAreaAnimation), "Client area animation");
        SystemParametersTweaks.SetMinimizeAnimation(false);
        SystemParametersTweaks.SetClientAreaAnimation(false);
        ActionLog.Success(Name, old, "OFF");
        return Task.FromResult(OperationResult.Ok($"{old} → {L.T("OFF")}"));
    }
}

public sealed class MouseAccelerationOptimization : Optimization
{
    public override string Id => "mouseaccel";
    public override string Name => L.T("Enhance pointer precision (mouse acceleration)");
    public override string Category => L.T("Input");
    public override string Icon => "";

    public override string WhatItDoes => L.T("Turns off Windows mouse acceleration so the same hand movement always moves the cursor the same distance.");

    public override string Downsides => L.T("Not an FPS change. Desktop cursor may feel slower at first – you might want to raise the mouse speed or DPI.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => false;

    protected override void RefreshCore(OptimizationContext context)
    {
        var values = SystemParametersTweaks.GetMouseParams();
        if (values is null)
        {
            IsAvailable = false;
            UnavailableReason = "This optimization is unavailable on your system. Mouse settings could not be read.";
            NeedsChange = false;
            CurrentState = Formatting.Unavailable;
            return;
        }

        bool on = values[2] != 0;
        CurrentState = OnOff(on);
        RecommendedState = "OFF (optional)";
        Reason = "Consistent aim in shooters like Fortnite. Fortnite uses raw input in-game, so this mainly affects menus and the desktop.";
        NeedsChange = on;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        string old = CurrentState;
        backup.CaptureSystemParameter(SystemParameterBackup.Mouse, SystemParameterBackup.Capture(SystemParameterBackup.Mouse), "Mouse acceleration");
        SystemParametersTweaks.SetMouseParams(new[] { 0, 0, 0 });
        ActionLog.Success(Name, old, "OFF");
        return Task.FromResult(OperationResult.Ok($"{old} → {L.T("OFF")}"));
    }
}
