using System;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;

namespace ConstiBoost.Models;

/// <summary>
/// One possible way to reduce unnecessary heat. Never promises a specific temperature drop –
/// the impact is a qualitative estimate (Low/Medium/High) based on the measured load it addresses.
/// </summary>
public sealed class ThermalFinding : ObservableObject
{
    private bool _isSelected;
    private string? _resultText;
    private bool? _resultSuccess;

    public int Priority { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Problem { get; init; } = string.Empty;
    public string Current { get; init; } = string.Empty;
    public string Recommended { get; init; } = string.Empty;
    public ImpactLevel Impact { get; init; }
    public string ImpactNote { get; init; } = "May reduce CPU load and therefore may reduce temperature.";
    public string? Tradeoff { get; init; }
    public bool IsReversible { get; init; }

    /// <summary>null = advice only (ConstiBoost cannot or should not change it automatically).</summary>
    public Func<BackupSession, Task<OperationResult>>? Apply { get; init; }

    public bool CanApply => Apply != null;
    public string ImpactText => Impact.ToString().ToUpperInvariant();
    public string KindText => CanApply ? (IsReversible ? "Can be applied · reversible" : "Can be applied") : "Advice only";

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value && CanApply); }
    public string? ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }
    public bool? ResultSuccess { get => _resultSuccess; set => SetProperty(ref _resultSuccess, value); }
}
