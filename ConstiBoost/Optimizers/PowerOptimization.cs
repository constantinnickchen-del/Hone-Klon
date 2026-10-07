using System;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Optimizers;

/// <summary>
/// Uses the Windows "Power mode" (Best performance) on the Balanced plan where available – this is what
/// Windows 11 laptops are designed for. Falls back to the built-in High performance plan if it exists.
/// </summary>
public sealed class PowerOptimization : Optimization
{
    private Guid? _scheme;
    private Guid? _overlay;
    private bool _highPerfExists;

    public override string Id => "power";
    public override string Name => "Power profile";
    public override string Category => "Power";
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.High;

    public override string WhatItDoes =>
        "Sets Windows to its performance power mode so CPU and integrated GPU can hold higher clocks instead of saving energy. Uses only built-in Windows power settings.";

    public override string Downsides =>
        "Higher power draw, more heat and fan noise, shorter battery life on laptops. Best used while plugged in.";

    protected override void RefreshCore(OptimizationContext context)
    {
        _scheme = PowerManager.GetActiveSchemeId();
        _overlay = PowerManager.GetOverlay();
        _highPerfExists = PowerManager.GetSchemes().Any(s => s.Id == PowerManager.HighPerformance || s.Id == PowerManager.UltimatePerformance);

        if (_scheme is null)
        {
            IsAvailable = false;
            UnavailableReason = "This optimization is unavailable on your system. The active power plan could not be read.";
            CurrentState = Formatting.Unavailable;
            NeedsChange = false;
            return;
        }

        string planName = PowerManager.GetFriendlyName(_scheme.Value);
        bool onBattery = context.Hardware.HasBattery && SystemInfo.GetPowerStatus().onAc == false;
        string batteryNote = onBattery ? " You are on battery right now – plug in for the full effect." : string.Empty;

        if (_scheme == PowerManager.HighPerformance || _scheme == PowerManager.UltimatePerformance)
        {
            CurrentState = planName;
            RecommendedState = planName;
            Reason = "Already using a performance power plan.";
            NeedsChange = false;
        }
        else if (_overlay != null && (_scheme == PowerManager.Balanced || _scheme == PowerManager.PowerSaver))
        {
            CurrentState = _scheme == PowerManager.Balanced ? $"Balanced · Power mode: {PowerManager.OverlayName(_overlay)}" : planName;
            RecommendedState = "Balanced · Power mode: Best performance";
            Reason = "The 'Best performance' power mode lets the CPU and Radeon/Intel graphics boost longer. This is the Windows-intended way on modern laptops." + batteryNote;
            NeedsChange = _scheme != PowerManager.Balanced || _overlay != PowerManager.OverlayBestPerformance;
        }
        else if (_highPerfExists)
        {
            CurrentState = planName;
            RecommendedState = "High performance";
            Reason = "The built-in High performance plan keeps clocks higher during games." + batteryNote;
            NeedsChange = true;
        }
        else
        {
            CurrentState = planName;
            RecommendedState = planName;
            Reason = "No additional built-in performance mode is available on this system.";
            NeedsChange = false;
        }
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        string old = CurrentState;
        backup.CapturePowerScheme();
        if (_overlay != null && (_scheme == PowerManager.Balanced || _scheme == PowerManager.PowerSaver))
        {
            backup.CapturePowerOverlay();
            if (_scheme != PowerManager.Balanced)
            {
                PowerManager.SetActiveScheme(PowerManager.Balanced);
            }

            PowerManager.SetOverlay(PowerManager.OverlayBestPerformance);
            if (PowerManager.GetOverlay() != PowerManager.OverlayBestPerformance)
            {
                ActionLog.Failed(Name, old, RecommendedState, "Windows did not accept the power mode");
                return Task.FromResult(OperationResult.Fail("This optimization couldn't be applied.", "Windows did not accept the power mode."));
            }
        }
        else if (_highPerfExists)
        {
            var target = PowerManager.GetSchemes().Any(s => s.Id == PowerManager.HighPerformance) ? PowerManager.HighPerformance : PowerManager.UltimatePerformance;
            PowerManager.SetActiveScheme(target);
        }

        ActionLog.Success(Name, old, RecommendedState);
        return Task.FromResult(OperationResult.Ok($"{old} → {RecommendedState}"));
    }
}
