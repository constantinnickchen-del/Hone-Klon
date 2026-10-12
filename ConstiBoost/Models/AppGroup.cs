using System.Collections.Generic;
using ConstiBoost.Core;
using ConstiBoost.Localization;

namespace ConstiBoost.Models;

/// <summary>All processes of one running app (e.g. 8× chrome.exe) shown as one row.</summary>
public sealed class AppGroup : ObservableObject
{
    private bool _isSelected;

    public string ProcessName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Path { get; init; }
    public List<int> ProcessIds { get; init; } = new();
    public long RamBytes { get; init; }
    public bool IsKnownBackgroundApp { get; init; }
    public bool HasWindow { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string RamText => Formatting.Bytes(RamBytes);

    public string Hint => IsKnownBackgroundApp
        ? L.T("Background app – safe to close while gaming")
        : L.T("Open app – closes like clicking X (you may be asked to save)");
}
