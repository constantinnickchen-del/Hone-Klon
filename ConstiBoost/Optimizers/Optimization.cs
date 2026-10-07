using System;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Optimizers;

public sealed class OptimizationContext
{
    public HardwareInfo Hardware { get; init; } = new();
    public DeviceProfile Profile { get; init; } = DeviceProfile.From(new HardwareInfo());
}

/// <summary>
/// One transparent optimization: shows current vs. recommended state, why, and the downsides.
/// Apply always records the previous state in a backup before changing anything.
/// </summary>
public abstract class Optimization : ObservableObject
{
    private string _currentState = "–";
    private string _recommendedState = "–";
    private string _reason = string.Empty;
    private bool _isAvailable = true;
    private string _unavailableReason = string.Empty;
    private bool _needsChange;
    private bool _isSelected;
    private string? _resultText;
    private bool? _resultSuccess;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string Icon { get; }
    public abstract string WhatItDoes { get; }
    public abstract string Downsides { get; }
    public virtual ImpactLevel Impact => ImpactLevel.Low;
    public virtual bool RequiresAdmin => false;
    public virtual bool IsReversible => true;

    /// <summary>Settings without a universally better value (e.g. HAGS). Never part of one-click optimize.</summary>
    public virtual bool IsUserChoice => false;

    /// <summary>Whether this optimization is pre-selected in one-click optimize when a change is needed.</summary>
    public virtual bool PreselectWhenNeeded(OptimizationContext context) => true;

    public string CurrentState { get => _currentState; protected set => SetProperty(ref _currentState, value); }
    public string RecommendedState { get => _recommendedState; protected set => SetProperty(ref _recommendedState, value); }
    public string Reason { get => _reason; protected set => SetProperty(ref _reason, value); }
    public bool IsAvailable { get => _isAvailable; protected set => SetProperty(ref _isAvailable, value); }
    public string UnavailableReason { get => _unavailableReason; protected set => SetProperty(ref _unavailableReason, value); }

    public bool NeedsChange
    {
        get => _needsChange;
        protected set
        {
            if (SetProperty(ref _needsChange, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public string? ResultText { get => _resultText; private set => SetProperty(ref _resultText, value); }
    public bool? ResultSuccess { get => _resultSuccess; private set => SetProperty(ref _resultSuccess, value); }

    public string ImpactText => Impact.ToString().ToUpperInvariant();
    public string ReversibleText => IsReversible ? "Reversible (backup)" : "Not reversible (deletes cache/temp files only)";
    public string StatusText => !IsAvailable ? "Unavailable" : IsUserChoice ? "Your choice" : NeedsChange ? "Can be improved" : "Optimal";

    /// <summary>Reads the current state. Runs on a background thread.</summary>
    public void Refresh(OptimizationContext context)
    {
        try
        {
            IsAvailable = true;
            UnavailableReason = string.Empty;
            RefreshCore(context);
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            NeedsChange = false;
            UnavailableReason = "This optimization is unavailable on your system. " + Safe.Describe(ex);
            AppLog.Warn($"Refresh of {Id} failed: {ex.Message}");
        }

        OnPropertyChanged(nameof(StatusText));
    }

    protected abstract void RefreshCore(OptimizationContext context);

    public async Task<OperationResult> ApplyAsync(BackupSession backup, OptimizationContext context)
    {
        if (!IsAvailable)
        {
            return OperationResult.Unavailable(UnavailableReason);
        }

        OperationResult result;
        try
        {
            result = await ApplyCoreAsync(backup, context);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Apply {Id} failed", ex);
            ActionLog.Failed(Name, CurrentState, RecommendedState, Safe.Describe(ex));
            result = OperationResult.Fail("This optimization couldn't be applied.", Safe.Describe(ex));
        }

        ResultSuccess = result.Success;
        ResultText = result.Success ? result.Message : result.Message + (result.TechnicalDetails is null ? string.Empty : $" ({result.TechnicalDetails})");
        Refresh(context);
        return result;
    }

    protected abstract Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context);

    /// <summary>Backs up, writes and verifies a per-user DWORD.</summary>
    protected OperationResult SetUserDword(BackupSession backup, string keyPath, string valueName, int value, string oldText, string newText, string? actionName = null)
    {
        backup.CaptureRegistry("HKCU", keyPath, valueName, actionName ?? Name);
        RegistryHelper.WriteDword("HKCU", keyPath, valueName, value);
        if (RegistryHelper.ReadDword("HKCU", keyPath, valueName) != value)
        {
            ActionLog.Failed(actionName ?? Name, oldText, newText, "Value could not be verified after writing");
            return OperationResult.Fail("This optimization couldn't be applied.", "Verification failed");
        }

        ActionLog.Success(actionName ?? Name, oldText, newText);
        return OperationResult.Ok($"{oldText} → {newText}");
    }

    protected static string OnOff(bool value) => value ? "ON" : "OFF";
}
