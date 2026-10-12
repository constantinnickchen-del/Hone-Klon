using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

/// <summary>"Adjust for best performance" style desktop effects via documented SystemParametersInfo settings.</summary>
public sealed class DesktopEffectsOptimization : Optimization
{
    // (name, get, set, value in uiParam)
    private static readonly (string name, uint get, uint set, bool ui)[] Effects =
    {
        ("Menu animations", 0x1002, 0x1003, false),
        ("Combo box animation", 0x1004, 0x1005, false),
        ("Smooth list scrolling", 0x1006, 0x1007, false),
        ("Selection fade", 0x1014, 0x1015, false),
        ("Tooltip animation", 0x1016, 0x1017, false),
        ("Cursor shadow", 0x101A, 0x101B, false),
        ("Window contents while dragging", 0x0026, 0x0025, true),
    };

    public override string Id => "desktopfx";
    public override string Name => L.T("Desktop effects (best performance)");
    public override string Category => L.T("Visual");
    public override string Icon => "";

    public override string WhatItDoes => L.T("Turns off menu, tooltip and list animations, cursor shadow, selection fade and live window contents while dragging – like 'Adjust for best performance' in System Properties, but font smoothing stays on.");

    public override string Downsides => L.T("Windows looks plainer and menus appear instantly. No functional change.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => context.Profile.IsLowPowerIntegrated;

    protected override void RefreshCore(OptimizationContext context)
    {
        var states = Effects.Select(e => (e.name, value: SystemParametersTweaks.GetBool(e.get))).ToList();
        int on = states.Count(s => s.value == true);
        CurrentState = states.All(s => s.value is null) ? Formatting.Unavailable : L.F("{0} of {1} effects on", on, Effects.Length);
        RecommendedState = context.Profile.IsLowPowerIntegrated ? "All off" : "Your choice";
        Reason = L.T("Less desktop GPU/CPU work on integrated graphics and a snappier feeling Windows.");
        NeedsChange = on > 0 && context.Profile.IsLowPowerIntegrated;
        if (states.All(s => s.value is null))
        {
            IsAvailable = false;
            UnavailableReason = L.T("This optimization is unavailable on your system. Effect settings could not be read.");
        }
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        int changed = 0;
        foreach (var (name, get, set, ui) in Effects)
        {
            if (SystemParametersTweaks.GetBool(get) != true)
            {
                continue;
            }

            string key = SystemParameterBackup.SpiName(get, set, ui);
            backup.CaptureSystemParameter(key, SystemParameterBackup.Capture(key), name);
            SystemParametersTweaks.SetBool(set, false, ui);
            changed++;
        }

        ActionLog.Success(Name, CurrentState, "All off");
        return Task.FromResult(OperationResult.Ok(L.F("{0} effect(s) turned off", changed)));
    }
}

/// <summary>Windows tips, suggestions and "get even more out of Windows" prompts (per user, reversible).</summary>
public sealed class SuggestionsOptimization : Optimization
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    private static readonly string[] Values =
    {
        "SubscribedContent-338389Enabled", // tips and suggestions
        "SubscribedContent-338388Enabled", // Start suggestions
        "SubscribedContent-310093Enabled", // welcome experience after updates
        "SubscribedContent-353694Enabled", // suggested content in Settings
        "SystemPaneSuggestionsEnabled",
        "SoftLandingEnabled",
    };

    public override string Id => "suggestions";
    public override string Name => L.T("Windows tips & suggestions");
    public override string Category => L.T("Background");
    public override string Icon => "";

    public override string WhatItDoes => L.T("Turns off Windows tips, Start menu suggestions and the post-update welcome screens (the same switches as in Settings > Notifications / Personalization).");

    public override string Downsides => L.T("You no longer get Windows tips. Security notifications and updates are not affected.");

    protected override void RefreshCore(OptimizationContext context)
    {
        int on = Values.Count(v => (RegistryHelper.ReadDword("HKCU", Key, v) ?? 1) != 0);
        CurrentState = L.F("{0} of {1} on", on, Values.Length);
        RecommendedState = "All off";
        Reason = L.T("Fewer background content downloads and pop-ups – small effect, but less distraction while gaming.");
        NeedsChange = on > 0;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        string old = CurrentState;
        foreach (string v in Values)
        {
            backup.CaptureRegistry("HKCU", Key, v, Name);
            RegistryHelper.WriteDword("HKCU", Key, v, 0);
        }

        ActionLog.Success(Name, old, "All off");
        return Task.FromResult(OperationResult.Ok(L.F("{0} → all off", old)));
    }
}
