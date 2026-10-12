using ConstiBoost.Core;
using ConstiBoost.Localization;

namespace ConstiBoost.Models;

public sealed class StartupEntry : ObservableObject
{
    private bool _isEnabled;
    private bool _isBusy;

    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public string Publisher { get; init; } = "Unknown publisher";
    public string Location { get; init; } = string.Empty;
    public string Impact { get; init; } = "Not measured";
    public bool IsProtected { get; init; }
    public string ProtectionReason { get; init; } = string.Empty;
    public bool RequiresAdmin { get; init; }
    public bool IsKnownNonEssential { get; init; }

    /// <summary>Where Windows stores the enabled/disabled flag (same mechanism Task Manager uses).</summary>
    public string ApprovedHive { get; init; } = "HKCU";
    public string ApprovedKeyPath { get; init; } = string.Empty;
    public string ApprovedValueName { get; init; } = string.Empty;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool CanToggle => !IsProtected;

    public string StatusText => IsEnabled ? L.T("Enabled") : L.T("Disabled");
}
