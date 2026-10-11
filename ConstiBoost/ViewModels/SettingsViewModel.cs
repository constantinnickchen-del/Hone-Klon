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
    public override string Title => "Settings";
    public override string Icon => "";

    public List<AccentOption> Accents { get; }

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
            ThermalMonitorService.Instance.SetAutomaticMonitoring(value);
            OnPropertyChanged();
        }
    }

    public string DataFolder => AppPaths.Root;

    public string VersionText => "ConstiBoost 1.0.0 · local only · no accounts · no telemetry";

    private void Save()
    {
        _settings.Save();
        ThemeService.Apply(_settings.Current.Theme, _settings.Current.AccentColor);
    }
}
