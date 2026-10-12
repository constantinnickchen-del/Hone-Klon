using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class AccentOption : ObservableObject
{
    private bool _isSelected;

    public AccentOption(string hex) => Hex = hex;

    public string Hex { get; }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public sealed class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings = SettingsService.Instance;

    public SettingsViewModel()
    {
        Accents = ThemeService.AccentPresets.Select(h => new AccentOption(h) { IsSelected = h == _settings.Current.AccentColor }).ToList();
        SelectAccentCommand = new RelayCommand(p =>
        {
            if (p is AccentOption option)
            {
                foreach (var a in Accents)
                {
                    a.IsSelected = ReferenceEquals(a, option);
                }

                _settings.Current.AccentColor = option.Hex;
                Save();
            }
        });
        ShowWelcomeCommand = new RelayCommand(() =>
        {
            if (MainViewModel.Instance != null)
            {
                MainViewModel.Instance.ShowWelcome = true;
            }
        });
    }

    public override string Key => "settings";
    public override string Title => Localization.L.T("Settings");
    public override string Icon => "";

    public List<AccentOption> Accents { get; }

    private readonly string _startLanguage = SettingsService.Instance.Current.Language;

    public bool IsGerman { get => _settings.Current.Language == "de"; set { if (value) SetLanguage("de"); } }
    public bool IsEnglish { get => _settings.Current.Language == "en"; set { if (value) SetLanguage("en"); } }
    public bool IsAutoLanguage { get => _settings.Current.Language is not ("de" or "en"); set { if (value) SetLanguage("auto"); } }
    public bool LanguageChanged => _settings.Current.Language != _startLanguage;
    public ICommand RestartCommand => new RelayCommand(App.Restart);

    private void SetLanguage(string language)
    {
        _settings.Current.Language = language;
        _settings.Save();
        OnPropertyChanged(nameof(IsGerman));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsAutoLanguage));
        OnPropertyChanged(nameof(LanguageChanged));
    }

    public ICommand SelectAccentCommand { get; }
    public ICommand ShowWelcomeCommand { get; }

    public bool IsDarkTheme
    {
        get => _settings.Current.Theme != "Midnight";
        set
        {
            if (value)
            {
                _settings.Current.Theme = "Dark";
                Save();
                OnPropertyChanged(nameof(IsMidnightTheme));
            }
        }
    }

    public bool IsMidnightTheme
    {
        get => _settings.Current.Theme == "Midnight";
        set
        {
            if (value)
            {
                _settings.Current.Theme = "Midnight";
                Save();
                OnPropertyChanged(nameof(IsDarkTheme));
            }
        }
    }

    public bool StartWithWindows
    {
        get => _settings.Current.StartWithWindows;
        set
        {
            var result = _settings.SetStartWithWindows(value);
            if (result.Success)
            {
                _settings.Current.StartWithWindows = value;
                _settings.Save();
            }
            else
            {
                NotificationService.Instance.Show(result.Message, NotificationKind.Error);
            }

            OnPropertyChanged();
        }
    }

    public bool CreateRestorePoint
    {
        get => _settings.Current.CreateRestorePointBeforeMajorChanges;
        set
        {
            _settings.Current.CreateRestorePointBeforeMajorChanges = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>Always on: ConstiBoost never changes a system setting without a backup.</summary>
    public bool AutomaticBackup => true;

    public bool Notifications
    {
        get => _settings.Current.Notifications;
        set
        {
            _settings.Current.Notifications = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool ThermalMonitoring
    {
        get => _settings.Current.ThermalMonitoringEnabled;
        set
        {
            _settings.Current.ThermalMonitoringEnabled = value;
            _settings.Save();
            ThermalMonitorService.Instance.SetAutomaticMonitoring(value || _settings.Current.AutoCoolEnabled);
            OnPropertyChanged();
        }
    }

    // ---------- Tray, hotkeys, overlay ----------
    public bool MinimizeToTray { get => _settings.Current.MinimizeToTray; set { _settings.Current.MinimizeToTray = value; _settings.Save(); OnPropertyChanged(); } }
    public bool HotkeysEnabled { get => _settings.Current.HotkeysEnabled; set { _settings.Current.HotkeysEnabled = value; _settings.Save(); OnPropertyChanged(); NotificationService.Instance.Show("Hotkey change applies after restarting ConstiBoost.", Models.NotificationKind.Info); } }
    public string HotkeyStatus => HotkeyService.Instance.Status;
    public bool OverlayShowsFps { get => _settings.Current.OverlayShowsFps; set { _settings.Current.OverlayShowsFps = value; _settings.Save(); OnPropertyChanged(); } }
    public bool OverlayTopLeft { get => _settings.Current.OverlayCorner == "TopLeft"; set { if (value) SetCorner("TopLeft"); } }
    public bool OverlayTopRight { get => _settings.Current.OverlayCorner == "TopRight"; set { if (value) SetCorner("TopRight"); } }
    public bool OverlayBottomLeft { get => _settings.Current.OverlayCorner == "BottomLeft"; set { if (value) SetCorner("BottomLeft"); } }
    public bool OverlayBottomRight { get => _settings.Current.OverlayCorner == "BottomRight"; set { if (value) SetCorner("BottomRight"); } }
    public ICommand ToggleOverlayCommand => new RelayCommand(() => OverlayService.Instance.Toggle());

    private void SetCorner(string corner)
    {
        _settings.Current.OverlayCorner = corner;
        _settings.Save();
        if (OverlayService.Instance.IsVisible)
        {
            OverlayService.Instance.Hide();
            OverlayService.Instance.Show();
        }

        OnPropertyChanged(string.Empty);
    }

    // ---------- Updates ----------
    public UpdateService Updates => UpdateService.Instance;
    public bool CheckForUpdates { get => _settings.Current.CheckForUpdates; set { _settings.Current.CheckForUpdates = value; _settings.Save(); OnPropertyChanged(); } }
    public ICommand CheckUpdatesCommand => new AsyncRelayCommand(() => UpdateService.Instance.CheckAsync(true));
    public ICommand InstallUpdateCommand => new AsyncRelayCommand(async () =>
    {
        if (UpdateService.Instance.Available is { } u && DialogService.Confirm($"Install ConstiBoost {u.Tag}?", "The installer is downloaded from GitHub Releases and started. ConstiBoost closes and restarts after the update. Your settings, backups and logs are kept.", string.IsNullOrWhiteSpace(u.Notes) ? null : new[] { u.Notes }, "UPDATE"))
        {
            await UpdateService.Instance.InstallAsync(new Progress<string>(t => NotificationService.Instance.Show(t, Models.NotificationKind.Info)));
        }
    });

    // ---------- Maintenance ----------
    public bool WeeklyMaintenance { get => _settings.Current.WeeklyMaintenance; set { _settings.Current.WeeklyMaintenance = value; _settings.Save(); OnPropertyChanged(); } }
    public string LastMaintenanceText => _settings.Current.LastMaintenance is DateTime d ? $"Last run: {d:dd.MM.yyyy HH:mm}" : "Never run";
    public ICommand RunMaintenanceCommand => new AsyncRelayCommand(async () =>
    {
        await MaintenanceService.Instance.RunAsync(false);
        OnPropertyChanged(nameof(LastMaintenanceText));
    });

    public string DataFolder => AppPaths.Root;

    public string VersionText => $"ConstiBoost {UpdateService.CurrentVersion.ToString(3)} PRO · local · no accounts · no telemetry";

    private void Save()
    {
        _settings.Save();
        ThemeService.Apply(_settings.Current.Theme, _settings.Current.AccentColor);
    }
}
