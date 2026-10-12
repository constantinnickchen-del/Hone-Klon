using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Localization;
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
            OnPropertyChanged(nameof(ActiveProfileText));
        };
        _capFortniteFps = Profile != GamingProfile.MaxFps;
    }

    private bool _capFortniteFps;

    public bool IsMaxFps
    {
        get => Profile == GamingProfile.MaxFps;
        set { if (value) Profile = GamingProfile.MaxFps; }
    }

    public bool IsBalanced
    {
        get => Profile == GamingProfile.Balanced;
        set { if (value) Profile = GamingProfile.Balanced; }
    }

    public bool IsCool
    {
        get => Profile == GamingProfile.Cool;
        set { if (value) Profile = GamingProfile.Cool; }
    }

    public GamingProfile Profile
    {
        get => SettingsService.Instance.Current.GamingProfile;
        set
        {
            SettingsService.Instance.Current.GamingProfile = value;
            SettingsService.Instance.Save();
            CapFortniteFps = value != GamingProfile.MaxFps;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMaxFps));
            OnPropertyChanged(nameof(IsBalanced));
            OnPropertyChanged(nameof(IsCool));
            OnPropertyChanged(nameof(ProfileDescription));
        }
    }

    public string ProfileDescription => Profile switch
    {
        GamingProfile.MaxFps => L.T("MAX FPS – maximum performance: 'Best performance' power mode. Higher temperature possible."),
        GamingProfile.Balanced => L.T("BALANCED – good FPS with less sustained heat: 'Balanced' power mode, CPU boost stays on. Optional FPS cap."),
        _ => L.T("COOL – lower heat, somewhat less performance: 'Balanced' power mode + maximum processor state 99 % (turns off CPU boost; can noticeably lower FPS in CPU-heavy scenes). Thermal protection stays fully active."),
    };

    public int FortniteFpsCap => ScanService.Instance.Last?.Hardware.Gpus.FirstOrDefault()?.RefreshRate is int hz and >= 30 and <= 240 ? hz : 60;

    /// <summary>Caps Fortnite's frame rate at the display refresh rate (only possible while Fortnite is closed).</summary>
    public bool CapFortniteFps { get => _capFortniteFps; set => SetProperty(ref _capFortniteFps, value); }

    public string ActiveProfileText => GamingModeService.Instance.ActiveProfile is GamingProfile p ? L.F("Profile: {0}", L.T(GamingModeService.ProfileName(p))) : string.Empty;

    public ICommand ToggleCommand { get; }
    public ICommand LoadAppsCommand { get; }

    public ObservableCollection<AppGroup> Apps { get; } = new();

    public bool IsActive => GamingModeService.Instance.IsActive;

    public string StatusText => IsActive
        ? L.F("Gaming Mode ACTIVE since {0:HH:mm}", GamingModeService.Instance.ActivatedAt)
        : L.T("Gaming Mode is off");

    public string ButtonText => IsActive ? L.T("STOP GAMING MODE") : L.T("START GAMING MODE");

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
                    messages.Count == 0 ? L.T("Gaming Mode stopped – settings restored.") : L.F("Gaming Mode stopped. Some items couldn't be restored: {0}", string.Join("; ", messages)),
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
                L.F("Profile: {0}", L.T(GamingModeService.ProfileName(Profile))),
                "Turn on Windows Game Mode",
                Profile == GamingProfile.MaxFps ? "Power mode: Best performance (if available)" : "Power mode: Balanced",
            };
            if (Profile == GamingProfile.Cool)
            {
                details.Add("Maximum processor state 99 % (CPU boost off) – may require administrator permission");
            }

            if (CapFortniteFps)
            {
                details.Add(L.F("Fortnite frame rate limit: {0} FPS (only if Fortnite is closed; previous value is restored)", FortniteFpsCap));
            }
            details.AddRange(selected.Select(a => L.F("Close {0}", a.DisplayName)));
            if (LowerPriority)
            {
                details.Add("Lower priority of browsers/launchers still running (Below normal)");
            }

            details.Add("Everything is restored when you stop Gaming Mode.");
            if (!DialogService.Confirm("Start Gaming Mode?", "ConstiBoost will make these temporary changes:", details, "START"))
            {
                return;
            }

            var result = await GamingModeService.Instance.ActivateAsync(selected, new GamingModeOptions(Profile, LowerPriority, CapFortniteFps ? FortniteFpsCap : null));
            ShowApps = false;
            NotificationService.Instance.Show(
                result.Count == 0 ? L.T("Gaming Mode ACTIVE. Have fun!") : L.F("Gaming Mode active with notes: {0}", string.Join("; ", result)),
                result.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
        }
        finally
        {
            IsBusy = false;
            Apps.Clear();
        }
    }
}
