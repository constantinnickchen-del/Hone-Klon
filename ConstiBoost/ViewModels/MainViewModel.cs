using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };
    private PageViewModel _currentPage;
    private string? _toastMessage;
    private NotificationKind _toastKind;
    private bool _isToastVisible;
    private bool _showWelcome;

    public MainViewModel()
    {
        Instance = this;
        Dashboard = new DashboardViewModel();
        Optimizer = new OptimizerViewModel();
        Fortnite = new FortniteViewModel();
        Thermal = new ThermalViewModel();
        Games = new GamesViewModel();
        BoostLab = new BoostLabViewModel();
        Cleaner = new CleanerViewModel();
        Startup = new StartupViewModel();
        Hardware = new HardwareViewModel();
        Backups = new BackupsViewModel();
        Settings = new SettingsViewModel();
        Pages = new List<PageViewModel> { Dashboard, Optimizer, BoostLab, Games, Thermal, Cleaner, Startup, Hardware, Backups, Settings };
        Welcome = new WelcomeViewModel(this);

        _currentPage = Dashboard;
        Dashboard.IsNavigationChecked = true;
        NavigateCommand = new RelayCommand(p => Navigate(p as string ?? "dashboard"));

        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            IsToastVisible = false;
        };
        NotificationService.Instance.Raised += ShowToast;
        DismissToastCommand = new RelayCommand(() => IsToastVisible = false);

        ShowWelcome = !SettingsService.Instance.Current.FirstRunCompleted;
    }

    public static MainViewModel? Instance { get; private set; }

    public DashboardViewModel Dashboard { get; }
    public OptimizerViewModel Optimizer { get; }
    public FortniteViewModel Fortnite { get; }
    public ThermalViewModel Thermal { get; }
    public GamesViewModel Games { get; }
    public BoostLabViewModel BoostLab { get; }
    public CleanerViewModel Cleaner { get; }
    public StartupViewModel Startup { get; }
    public HardwareViewModel Hardware { get; }
    public BackupsViewModel Backups { get; }
    public SettingsViewModel Settings { get; }
    public WelcomeViewModel Welcome { get; }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public ICommand NavigateCommand { get; }
    public ICommand DismissToastCommand { get; }

    public string AdminText => SystemInfo.IsAdministrator ? "Administrator" : "Standard user · UAC on demand";

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public bool ShowWelcome
    {
        get => _showWelcome;
        set => SetProperty(ref _showWelcome, value);
    }

    public string? ToastMessage { get => _toastMessage; private set => SetProperty(ref _toastMessage, value); }
    public NotificationKind ToastKind { get => _toastKind; private set => SetProperty(ref _toastKind, value); }
    public bool IsToastVisible { get => _isToastVisible; private set => SetProperty(ref _isToastVisible, value); }

    public string ToastIcon => ToastKind switch
    {
        NotificationKind.Success => "",
        NotificationKind.Warning => "",
        NotificationKind.Error => "",
        _ => "",
    };

    public void Navigate(string key)
    {
        var target = Pages.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (target is null || ReferenceEquals(target, CurrentPage))
        {
            return;
        }

        CurrentPage.OnNavigatedFrom();
        CurrentPage.IsNavigationChecked = false;
        target.IsNavigationChecked = true;
        CurrentPage = target;
        target.OnNavigatedTo();
    }

    private bool _suspended;

    public void Start(bool minimized)
    {
        if (minimized)
        {
            _suspended = true; // pages start when the window is restored
            return;
        }

        CurrentPage.OnNavigatedTo();
    }

    /// <summary>Stops live monitoring/polling while the window is minimized (keeps ConstiBoost idle).</summary>
    public void Suspend()
    {
        if (!_suspended)
        {
            _suspended = true;
            CurrentPage.OnNavigatedFrom();
        }
    }

    public void Resume()
    {
        if (_suspended)
        {
            _suspended = false;
            CurrentPage.OnNavigatedTo();
        }
    }

    private void ShowToast(Notification notification)
    {
        ToastMessage = notification.Message;
        ToastKind = notification.Kind;
        OnPropertyChanged(nameof(ToastIcon));
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Interval = notification.Kind is NotificationKind.Error or NotificationKind.Warning ? TimeSpan.FromSeconds(7) : TimeSpan.FromSeconds(4.5);
        _toastTimer.Start();
    }
}
