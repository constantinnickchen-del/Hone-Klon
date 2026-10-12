using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Fortnite;
using ConstiBoost.Games;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed record ResolutionOption(string Label, int Width, int Height);

/// <summary>🎮 Games: every installed game, last played, uninstall, FPS estimates and Game Focus.</summary>
public sealed class GamesViewModel : PageViewModel
{
    private readonly DispatcherTimer _runningTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isScanning;
    private bool _scanned;
    private string _search = string.Empty;
    private string _sort = "LastPlayed";
    private GameDetailViewModel? _detail;
    private GameInfo? _expanded;

    public GamesViewModel()
    {
        GamesView = CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = o => o is GameInfo g && (string.IsNullOrWhiteSpace(_search) || g.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) || g.SourceText.Contains(_search, StringComparison.OrdinalIgnoreCase));
        ApplySort();
        RescanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        ToggleCommand = new RelayCommand(p => Toggle(p as GameInfo));
        SortCommand = new RelayCommand(p => { _sort = p as string ?? "Name"; ApplySort(); OnPropertyChanged(nameof(SortText)); });
        _runningTimer.Tick += async (_, _) => await UpdateRunningAsync();
        GameFocusService.Instance.Changed += () => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            foreach (var g in Games)
            {
                g.IsFocused = g.Id == GameFocusService.Instance.FocusedGameId;
            }

            _detail?.RaiseFocus();
            OnPropertyChanged(nameof(FocusText));
        });
    }

    public override string Key => "games";
    public override string Title => "Games";
    public override string Icon => "";

    public ObservableCollection<GameInfo> Games { get; } = new();
    public ICollectionView GamesView { get; }

    public ICommand RescanCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand SortCommand { get; }

    public bool IsScanning { get => _isScanning; private set => SetProperty(ref _isScanning, value); }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
            {
                GamesView.Refresh();
            }
        }
    }

    public string SortText => _sort switch { "Name" => "A–Z", "Size" => "Size", _ => "Last played" };

    public string SummaryText => Games.Count == 0
        ? "No games found yet."
        : $"{Games.Count} games · {Formatting.Bytes(Games.Sum(g => g.SizeBytes ?? 0))} on disk · Steam {Games.Count(g => g.Source == GameSource.Steam)} · Epic {Games.Count(g => g.Source == GameSource.Epic)} · Store/Xbox {Games.Count(g => g.Source is GameSource.Store or GameSource.Xbox)} · other {Games.Count(g => g.Source is GameSource.Installed or GameSource.Minecraft)}";

    public string FocusText => GameFocusService.Instance.State is { } s ? $"🎯 Focus active: {s.GameName} (since {s.Started:HH:mm})" : string.Empty;

    public GameDetailViewModel? Detail { get => _detail; private set => SetProperty(ref _detail, value); }

    public override void OnNavigatedTo()
    {
        if (!_scanned && !IsScanning)
        {
            _ = ScanAsync();
        }

        _runningTimer.Start();
        Detail?.Activate();
    }

    public override void OnNavigatedFrom()
    {
        _runningTimer.Stop();
        Detail?.Deactivate();
    }

    /// <summary>Opens the Games page with a game expanded (e.g. "Fortnite Boost" on the Dashboard).</summary>
    public async void OpenGame(Func<GameInfo, bool> predicate)
    {
        MainViewModel.Instance?.Navigate(Key);
        if (!_scanned)
        {
            await ScanAsync();
        }

        var game = Games.FirstOrDefault(predicate);
        if (game != null && !game.IsExpanded)
        {
            Toggle(game);
        }
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            var list = await Task.Run(() =>
            {
                var games = GameLibraryService.Scan();
                LastPlayedService.Apply(games);
                return games;
            });

            string? expandedId = _expanded?.Id;
            Games.Clear();
            foreach (var g in list)
            {
                g.IsFocused = g.Id == GameFocusService.Instance.FocusedGameId;
                Games.Add(g);
            }

            ThermalMonitorService.RegisterLibraryGames(list.Where(g => g.MainExecutable != null).Select(g => Path.GetFileNameWithoutExtension(g.MainExecutable!)));
            _scanned = true;
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(FocusText));
            ApplySort();
            if (expandedId != null && Games.FirstOrDefault(g => g.Id == expandedId) is { } again)
            {
                _expanded = null;
                Toggle(again);
            }

            _ = Task.Run(FillMissingSizes);
            await UpdateRunningAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Game scan failed", ex);
            NotificationService.Instance.Show("The game library couldn't be read completely. Details were written to the log.", NotificationKind.Warning);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void FillMissingSizes()
    {
        foreach (var g in Games.ToList().Where(g => g.SizeBytes is null))
        {
            long size = GameLibraryService.FolderSize(g.InstallPath);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                g.SizeBytes = size > 0 ? size : null;
                OnPropertyChanged(nameof(SummaryText));
            });
        }
    }

    private async Task UpdateRunningAsync()
    {
        var games = Games.ToList();
        if (games.Count == 0)
        {
            return;
        }

        var running = await Task.Run(() =>
        {
            var roots = games.SelectMany(g => g.ProcessRoots.Select(r => (g, root: GameLibraryService.NormalizePath(r) + "\\"))).ToList();
            var ids = new HashSet<string>();
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                using (p)
                {
                    string? path = WinSys.ProcessSafety.TryGetPath(p.Id);
                    if (path is null)
                    {
                        continue;
                    }

                    foreach (var (g, root) in roots)
                    {
                        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        {
                            ids.Add(g.Id);
                        }
                    }
                }
            }

            return ids;
        });

        foreach (var g in games)
        {
            bool now = running.Contains(g.Id);
            if (now)
            {
                LastPlayedService.RecordSession(g.Id);
                g.LastPlayed = DateTime.Now;
                g.LastPlayedSource = "ConstiBoost";
            }

            g.IsRunning = now;
        }
    }

    private void Toggle(GameInfo? game)
    {
        if (game is null)
        {
            return;
        }

        if (game.IsExpanded)
        {
            game.IsExpanded = false;
            Detail?.Deactivate();
            Detail = null;
            _expanded = null;
            return;
        }

        if (_expanded != null)
        {
            _expanded.IsExpanded = false;
            Detail?.Deactivate();
        }

        game.IsExpanded = true;
        _expanded = game;
        Detail = new GameDetailViewModel(game);
        Detail.Activate();
    }

    private void ApplySort()
    {
        GamesView.SortDescriptions.Clear();
        switch (_sort)
        {
            case "Name":
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameInfo.Name), ListSortDirection.Ascending));
                break;
            case "Size":
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameInfo.SizeBytes), ListSortDirection.Descending));
                break;
            default:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameInfo.LastPlayed), ListSortDirection.Descending));
                GamesView.SortDescriptions.Add(new SortDescription(nameof(GameInfo.Name), ListSortDirection.Ascending));
                break;
        }
    }
}

/// <summary>Expanded game: estimates, focus options and actions.</summary>
public sealed class GameDetailViewModel : ObservableObject
{
    private readonly GameProfile? _profile;
    private EstimateContext? _context;
    private ResolutionOption _resolution;
    private int _scale = 100;
    private bool _boostPriority = true;
    private bool _gpuPreference;
    private bool _muteNotifications = true;
    private bool _lowerPriority = true;
    private bool _capFps;
    private GamingProfile _focusProfile;
    private bool _isBusy;
    private string _currentSetting = string.Empty;

    public GameDetailViewModel(GameInfo game)
    {
        Game = game;
        _profile = GameDatabase.Get(game.ProfileKey);
        var hw = ScanService.Instance.Last?.Hardware;
        int w = hw?.Gpus.FirstOrDefault()?.HorizontalResolution ?? 1920;
        int h = hw?.Gpus.FirstOrDefault()?.VerticalResolution ?? 1080;
        Resolutions = new List<ResolutionOption> { new("1280×720", 1280, 720), new("1600×900", 1600, 900), new("1920×1080", 1920, 1080) };
        if (!Resolutions.Any(r => r.Width == w && r.Height == h) && w >= 1024)
        {
            Resolutions.Add(new ResolutionOption($"{w}×{h} (native)", w, h));
        }

        _resolution = Resolutions.FirstOrDefault(r => r.Width == w && r.Height == h) ?? Resolutions[2];
        _gpuPreference = (hw?.Gpus.Count ?? 1) > 1;
        _focusProfile = SettingsService.Instance.Current.GamingProfile;
        _capFps = game.IsFortnite && _focusProfile != GamingProfile.MaxFps;

        LaunchCommand = new RelayCommand(() => Report(GameActions.Launch(Game)));
        UninstallCommand = new RelayCommand(Uninstall);
        OpenFolderCommand = new RelayCommand(() => GameActions.OpenFolder(Game));
        FocusCommand = new AsyncRelayCommand(FocusAsync, () => !IsBusy);
        LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
        SelectResolutionCommand = new RelayCommand(p =>
        {
            if (p is ResolutionOption r)
            {
                Resolution = r;
            }
        });
    }

    public GameInfo Game { get; }

    public FortniteViewModel? Fortnite => Game.IsFortnite ? MainViewModel.Instance?.Fortnite : null;
    public bool IsFortnite => Game.IsFortnite;

    public ICommand LaunchCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand FocusCommand { get; }
    public ICommand LoadAppsCommand { get; }
    public ICommand SelectResolutionCommand { get; }

    public List<ResolutionOption> Resolutions { get; }
    public ObservableCollection<FpsEstimate> Estimates { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> Factors { get; } = new();
    public ObservableCollection<AppGroup> Apps { get; } = new();

    public bool HasEstimate => _profile != null && _context?.Gpu != null;
    public string EstimateHeader => _profile is null
        ? "No FPS estimate data for this game yet."
        : _context?.Gpu is null ? "Your graphics chip is not in the estimate database – no FPS estimate shown." : $"Estimated FPS for {_profile.Name} on this PC";
    public string Tip => _profile?.Tip ?? "Lower resolution, shadows and effects first – they cost the most on integrated graphics.";
    public string CurrentSetting { get => _currentSetting; private set => SetProperty(ref _currentSetting, value); }

    public ResolutionOption Resolution
    {
        get => _resolution;
        set
        {
            if (SetProperty(ref _resolution, value))
            {
                UpdateEstimates();
            }
        }
    }

    public int Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, Math.Clamp(value, 50, 100)))
            {
                UpdateEstimates();
            }
        }
    }

    public bool BoostPriority { get => _boostPriority; set => SetProperty(ref _boostPriority, value); }
    public bool GpuPreference { get => _gpuPreference; set => SetProperty(ref _gpuPreference, value); }
    public bool MuteNotifications { get => _muteNotifications; set => SetProperty(ref _muteNotifications, value); }
    public bool LowerPriority { get => _lowerPriority; set => SetProperty(ref _lowerPriority, value); }
    public bool CapFps { get => _capFps; set => SetProperty(ref _capFps, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public bool IsMaxFps { get => _focusProfile == GamingProfile.MaxFps; set { if (value) SetProfile(GamingProfile.MaxFps); } }
    public bool IsBalanced { get => _focusProfile == GamingProfile.Balanced; set { if (value) SetProfile(GamingProfile.Balanced); } }
    public bool IsCool { get => _focusProfile == GamingProfile.Cool; set { if (value) SetProfile(GamingProfile.Cool); } }

    public int FpsCap => ScanService.Instance.Last?.Hardware.Gpus.FirstOrDefault()?.RefreshRate is int hz and >= 30 and <= 240 ? hz : 60;

    public bool IsFocused => GameFocusService.Instance.FocusedGameId == Game.Id;
    public string FocusButtonText => IsFocused ? "STOP FOCUS" : "🎯 FOCUS ON THIS GAME";

    public void Activate()
    {
        var hw = ScanService.Instance.Last?.Hardware;
        if (hw != null)
        {
            _context = FpsEstimator.BuildContext(hw, MonitorService.Instance.Last?.Sensors.BestTemperature);
        }

        if (Game.IsFortnite)
        {
            Fortnite?.OnNavigatedTo();
            ReadFortniteSettings();
        }

        UpdateEstimates();
        OnPropertyChanged(nameof(HasEstimate));
        OnPropertyChanged(nameof(EstimateHeader));
        _ = LoadAppsAsync();
    }

    public void Deactivate()
    {
        if (Game.IsFortnite)
        {
            Fortnite?.OnNavigatedFrom();
        }
    }

    public void RaiseFocus()
    {
        OnPropertyChanged(nameof(IsFocused));
        OnPropertyChanged(nameof(FocusButtonText));
    }

    private void SetProfile(GamingProfile profile)
    {
        _focusProfile = profile;
        CapFps = Game.IsFortnite && profile != GamingProfile.MaxFps;
        OnPropertyChanged(nameof(IsMaxFps));
        OnPropertyChanged(nameof(IsBalanced));
        OnPropertyChanged(nameof(IsCool));
    }

    private string? _fortnitePreset;

    private void ReadFortniteSettings()
    {
        var cfg = FortniteConfigService.Read();
        if (!cfg.Exists)
        {
            CurrentSetting = "Fortnite settings file not found – start Fortnite once.";
            return;
        }

        string mode = cfg.RenderingMode;
        double? avg = new[] { "sg.ViewDistanceQuality", "sg.ShadowQuality", "sg.EffectsQuality", "sg.TextureQuality", "sg.PostProcessQuality" }
            .Select(k => double.TryParse(cfg.Get(k), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (double?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(-1).Average();
        _fortnitePreset = mode == "Performance" ? "Performance Mode · Low"
            : mode == "DirectX 12" ? (avg >= 2.5 ? "DirectX 12 · Epic (Lumen)" : "DirectX 12 · High")
            : avg >= 1 ? "DirectX 11 · Medium" : "DirectX 11 · Low";
        if (double.TryParse(cfg.Get("sg.ResolutionQuality"), NumberStyles.Float, CultureInfo.InvariantCulture, out double res) && res >= 50)
        {
            _scale = (int)Math.Round(res);
            OnPropertyChanged(nameof(Scale));
        }

        int.TryParse(cfg.Get("ResolutionSizeX"), out int rx);
        int.TryParse(cfg.Get("ResolutionSizeY"), out int ry);
        if (rx >= 640 && ry >= 480)
        {
            var match = Resolutions.FirstOrDefault(r => r.Width == rx && r.Height == ry);
            if (match is null)
            {
                match = new ResolutionOption($"{rx}×{ry} (in-game)", rx, ry);
                Resolutions.Add(match);
                OnPropertyChanged(nameof(Resolutions));
            }

            _resolution = match;
            OnPropertyChanged(nameof(Resolution));
        }

        CurrentSetting = $"Your current Fortnite settings: {mode} · ~{_fortnitePreset} · {_resolution.Label} · 3D resolution {_scale} %";
    }

    private void UpdateEstimates()
    {
        Estimates.Clear();
        Factors.Clear();
        if (_profile is null || _context is null)
        {
            return;
        }

        foreach (var (name, value) in FpsEstimator.Factors(_context))
        {
            Factors.Add(new KeyValuePair<string, string>(name, value));
        }

        foreach (var preset in _profile.Presets)
        {
            var e = FpsEstimator.Estimate(_profile, preset, _resolution.Width, _resolution.Height, _scale / 100.0, _context, preset.Name == _fortnitePreset);
            if (e != null)
            {
                Estimates.Add(e);
            }
        }
    }

    private async Task LoadAppsAsync()
    {
        var groups = await Task.Run(BackgroundAppsService.GetCandidates);
        Apps.Clear();
        string? gameRoot = GameLibraryService.NormalizePath(Game.InstallPath) + "\\";
        foreach (var g in groups.Where(g => g.Path is null || !g.Path.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)))
        {
            bool launcherForThisGame = (Game.Source == GameSource.Steam && g.ProcessName.StartsWith("steam", StringComparison.OrdinalIgnoreCase)) ||
                                       (Game.Source == GameSource.Epic && g.ProcessName.StartsWith("EpicGames", StringComparison.OrdinalIgnoreCase)) ||
                                       (Game.Source == GameSource.Minecraft && g.ProcessName.Contains("Minecraft", StringComparison.OrdinalIgnoreCase)) ||
                                       (Game.Source == GameSource.Xbox && g.ProcessName.StartsWith("XboxPcApp", StringComparison.OrdinalIgnoreCase));
            g.IsSelected = g.IsKnownBackgroundApp && !launcherForThisGame && !BackgroundAppsService.KeepPriority.Contains(g.ProcessName) &&
                           !BackgroundAppsService.BrowserProcesses.Contains(g.ProcessName);
            Apps.Add(g);
        }
    }

    private async Task FocusAsync()
    {
        IsBusy = true;
        try
        {
            if (IsFocused)
            {
                var stopMessages = await GameFocusService.Instance.StopAsync();
                NotificationService.Instance.Show(stopMessages.Count == 0 ? "Game Focus stopped – everything restored." : "Game Focus stopped with notes: " + string.Join("; ", stopMessages),
                    stopMessages.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
                return;
            }

            var apps = Apps.Where(a => a.IsSelected).ToList();
            var details = new List<string>
            {
                $"Gaming Mode profile: {GamingModeService.ProfileName(_focusProfile)} (Game Mode ON, power settings for the profile)",
            };
            details.AddRange(apps.Select(a => "Close " + a.DisplayName));
            if (LowerPriority) details.Add("Lower priority of browsers/launchers still running");
            if (BoostPriority) details.Add($"{Game.Name}: priority 'Above normal' while it runs (anti-cheat may block this – harmless)");
            if (GpuPreference && Game.MainExecutable != null) details.Add($"Windows graphics preference 'High performance' for {Path.GetFileName(Game.MainExecutable)}");
            if (MuteNotifications) details.Add("Mute Windows notification banners");
            if (CapFps && Game.IsFortnite) details.Add($"Fortnite frame rate limit {FpsCap} FPS (only while Fortnite is closed)");
            details.Add("Everything is restored when you press STOP FOCUS.");
            if (!DialogService.Confirm($"Focus on {Game.Name}?", "ConstiBoost puts all safe system resources behind this game:", details, "START FOCUS"))
            {
                return;
            }

            var messages = await GameFocusService.Instance.StartAsync(Game, new FocusOptions(_focusProfile, apps, LowerPriority, BoostPriority, GpuPreference, MuteNotifications, CapFps ? FpsCap : null));
            NotificationService.Instance.Show(messages.Count == 0 ? $"🎯 Focus on {Game.Name} active. Have fun!" : "Focus active with notes: " + string.Join("; ", messages),
                messages.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
            await LoadAppsAsync();
        }
        finally
        {
            IsBusy = false;
            RaiseFocus();
        }
    }

    private void Uninstall()
    {
        if (!DialogService.Confirm($"Uninstall {Game.Name}?",
                "ConstiBoost starts the official uninstaller (Steam, Epic or the game's own uninstaller). Game files are never deleted by ConstiBoost itself. Cloud saves are usually kept, local saves may be removed by the uninstaller.",
                new[] { Game.InstallPath, Game.SizeText }, "UNINSTALL", danger: true))
        {
            return;
        }

        Report(GameActions.Uninstall(Game));
    }

    private static void Report(OperationResult r) =>
        NotificationService.Instance.Show(r.Message, r.Success ? NotificationKind.Success : NotificationKind.Warning);
}
