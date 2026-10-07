using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

/// <summary>🎮 GAMING MODE – shared by Dashboard and Fortnite pages.</summary>
public sealed class GamingModeViewModel : ObservableObject
{
    private bool _isBusy;
    private bool _lowerPriority = true;
    private bool _showApps;

    public static GamingModeViewModel Shared { get; } = new();

    private GamingModeViewModel()
    {
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => !IsBusy);
        LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
        GamingModeService.Instance.Changed += () =>
        {
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ButtonText));
        };
    }

    public ICommand ToggleCommand { get; }
    public ICommand LoadAppsCommand { get; }

    public ObservableCollection<AppGroup> Apps { get; } = new();

    public bool IsActive => GamingModeService.Instance.IsActive;

    public string StatusText => IsActive
        ? $"Gaming Mode ACTIVE since {GamingModeService.Instance.ActivatedAt:HH:mm}"
        : "Gaming Mode is off";

    public string ButtonText => IsActive ? "STOP GAMING MODE" : "START GAMING MODE";

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool LowerPriority { get => _lowerPriority; set => SetProperty(ref _lowerPriority, value); }
    public bool ShowApps { get => _showApps; set => SetProperty(ref _showApps, value); }

    public bool RelaunchClosedApps
    {
        get => SettingsService.Instance.Current.GamingModeRelaunchClosedApps;
        set
        {
            SettingsService.Instance.Current.GamingModeRelaunchClosedApps = value;
            SettingsService.Instance.Save();
            OnPropertyChanged();
        }
    }

    private async Task LoadAppsAsync()
    {
        var groups = await Task.Run(BackgroundAppsService.GetCandidates);
        Apps.Clear();
        foreach (var g in groups)
        {
            g.IsSelected = g.IsKnownBackgroundApp && !BackgroundAppsService.BrowserProcesses.Contains(g.ProcessName);
            Apps.Add(g);
        }

        ShowApps = true;
    }

    private async Task ToggleAsync()
    {
        IsBusy = true;
        try
        {
            if (IsActive)
            {
                var messages = await GamingModeService.Instance.DeactivateAsync(RelaunchClosedApps);
                NotificationService.Instance.Show(
                    messages.Count == 0 ? "Gaming Mode stopped – settings restored." : "Gaming Mode stopped. Some items couldn't be restored: " + string.Join("; ", messages),
                    messages.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
                return;
            }

            if (Apps.Count == 0)
            {
                await LoadAppsAsync();
            }

            var selected = Apps.Where(a => a.IsSelected).ToList();
            var details = new System.Collections.Generic.List<string>
            {
                "Turn on Windows Game Mode",
                "Switch to the performance power mode (if available)",
            };
            details.AddRange(selected.Select(a => "Close " + a.DisplayName));
            if (LowerPriority)
            {
                details.Add("Lower priority of browsers/launchers still running (Below normal)");
            }

            details.Add("Everything is restored when you stop Gaming Mode.");
            if (!DialogService.Confirm("Start Gaming Mode?", "ConstiBoost will make these temporary changes:", details, "START"))
            {
                return;
            }

            var result = await GamingModeService.Instance.ActivateAsync(selected, LowerPriority);
            ShowApps = false;
            NotificationService.Instance.Show(
                result.Count == 0 ? "Gaming Mode ACTIVE. Have fun!" : "Gaming Mode active with notes: " + string.Join("; ", result),
                result.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
        }
        finally
        {
            IsBusy = false;
            Apps.Clear();
        }
    }
}
