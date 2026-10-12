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
using ConstiBoost.Backup;
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
    public override string Title => Localization.L.T("Games");
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
public sealed record FlagPage(string Url, string What, ICommand CopyCommand);

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
        if (game.IsBrowser)
        {
            _focusProfile = GamingProfile.MaxFps;
            _boostPriority = false; // would also raise every background tab
            _gpuPreference = true;
        }

        if (AutoProfileService.Instance.Get(game.Id) is { } saved)
        {
            _focusProfile = saved.Profile;
            _boostPriority = saved.BoostPriority;
            _gpuPreference = saved.GpuPreference;
            _muteNotifications = saved.MuteNotifications;
            _lowerPriority = saved.LowerPriority;
            _capFps = saved.CapFps;
        }

        LaunchCommand = new RelayCommand(() => Report(GameActions.Launch(Game)));
        AnalyzeBottleneckCommand = new AsyncRelayCommand(AnalyzeBottleneckAsync, () => !IsAnalyzing);
        PerformanceLaunchCommand = new AsyncRelayCommand(PerformanceLaunchAsync, () => !IsBusy);
        SelectRenderScaleCommand = new RelayCommand(p =>
        {
            if (int.TryParse(p as string, out int v))
            {
                RenderScale = v;
            }
        });
        UninstallCommand = new RelayCommand(Uninstall);
        OpenFolderCommand = new RelayCommand(() => GameActions.OpenFolder(Game));
        FocusCommand = new AsyncRelayCommand(FocusAsync, () => !IsBusy);
        LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
        MeasureCommand = new AsyncRelayCommand(p => MeasureAsync(int.TryParse(p as string, out int sec) ? sec : 60), _ => !IsMeasuring);
        CancelMeasureCommand = new RelayCommand(() => _measureCts?.Cancel(), () => IsMeasuring);
        AutoProfile = AutoProfileService.Instance.Get(game.Id);
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
    public bool IsBrowser => Game.IsBrowser;
    public string AccelerationUrl => BrowserGamesScanner.AccelerationSettingsUrl(Game);
    public string GpuUrl => BrowserGamesScanner.GpuInfoUrl(Game);
    public ICommand CopyAccelerationUrlCommand => new RelayCommand(() => CopyToClipboard(AccelerationUrl));
    public ICommand CopyGpuUrlCommand => new RelayCommand(() => CopyToClipboard(GpuUrl));

    private static void CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text.Split(' ')[0]);
            NotificationService.Instance.Show($"Copied: {text.Split(' ')[0]} – paste it into the browser's address bar.", NotificationKind.Info);
        }
        catch
        {
            NotificationService.Instance.Show("Clipboard busy – type the address manually: " + text, NotificationKind.Warning);
        }
    }

    public ICommand LaunchCommand { get; }
    public ICommand AnalyzeBottleneckCommand { get; }
    public ICommand PerformanceLaunchCommand { get; }
    public ICommand SelectRenderScaleCommand { get; }

    // ---------- Bottleneck check ----------
    private bool _isAnalyzing;
    private string _bottleneckStatus = string.Empty;
    private BottleneckReport? _bottleneck;

    public bool IsAnalyzing { get => _isAnalyzing; private set { if (SetProperty(ref _isAnalyzing, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string BottleneckStatus { get => _bottleneckStatus; private set => SetProperty(ref _bottleneckStatus, value); }
    public BottleneckReport? Bottleneck { get => _bottleneck; private set { if (SetProperty(ref _bottleneck, value)) OnPropertyChanged(nameof(HasBottleneck)); } }
    public bool HasBottleneck => _bottleneck != null;

    private async Task AnalyzeBottleneckAsync()
    {
        IsAnalyzing = true;
        Bottleneck = null;
        try
        {
            var progress = new Progress<string>(t => BottleneckStatus = t);
            var report = await BottleneckService.AnalyzeAsync(12, progress, System.Threading.CancellationToken.None, Game);
            Bottleneck = report;
            BottleneckStatus = report.Findings.Count == 0
                ? report.Summary + "\nNo clear limit found – run it again while you actively play."
                : report.Summary;
        }
        catch (Exception ex)
        {
            BottleneckStatus = "Analysis failed: " + Safe.Describe(ex);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    // ---------- Browser Performance Launch ----------
    public bool CanPerformanceLaunch => BrowserLaunchService.IsSupported(Game);
    public bool IsFirefox => Game.IsBrowser && !CanPerformanceLaunch;

    public string LaunchUrl
    {
        get => SettingsService.Instance.Current.BrowserLaunchUrl;
        set { SettingsService.Instance.Current.BrowserLaunchUrl = value; OnPropertyChanged(); }
    }

    public int RenderScale
    {
        get => SettingsService.Instance.Current.BrowserRenderScale;
        set
        {
            SettingsService.Instance.Current.BrowserRenderScale = value;
            SettingsService.Instance.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(RenderScaleText));
        }
    }

    public bool IsScale100 => RenderScale >= 100;
    public bool IsScale85 => RenderScale == 85;
    public bool IsScale75 => RenderScale == 75;
    public bool IsScale60 => RenderScale == 60;

    public string RenderScaleText
    {
        get
        {
            int s = RenderScale;
            double pixels = s / 100.0 * (s / 100.0) * 100;
            return s >= 100 ? "Full window (maximized) – most pixels, lowest FPS." : $"Window {s} % of the screen → ~{pixels:0} % of the pixels to render. Press F11 in the window for borderless full screen only if you want 100 % again.";
        }
    }

    public bool UnlockFps
    {
        get => SettingsService.Instance.Current.BrowserUnlockFps;
        set { SettingsService.Instance.Current.BrowserUnlockFps = value; SettingsService.Instance.Save(); OnPropertyChanged(); }
    }

    public IReadOnlyList<FlagPage> FlagPages => BrowserLaunchService.IsSupported(Game)
        ? BrowserLaunchService.GpuFlagPages(Game).Select(f => new FlagPage(f.Url, f.What, new RelayCommand(() => CopyToClipboard(f.Url)))).ToList()
        : new List<FlagPage>();

    public bool LaunchStartsFocus
    {
        get => SettingsService.Instance.Current.BrowserLaunchStartsFocus;
        set { SettingsService.Instance.Current.BrowserLaunchStartsFocus = value; SettingsService.Instance.Save(); OnPropertyChanged(); }
    }

    private async Task PerformanceLaunchAsync()
    {
        if (!BrowserLaunchService.TryNormalizeUrl(LaunchUrl, out string url))
        {
            NotificationService.Instance.Show("Please enter the web address of the game first (e.g. https://…).", NotificationKind.Warning);
            return;
        }

        LaunchUrl = url;
        SettingsService.Instance.Save();
        const bool separate = false; // always the normal browser profile
        // As administrator the game is handed to the browser through Explorer (no restart needed / possible).
        bool fresh = WinSys.SystemInfo.IsAdministrator || !BrowserLaunchService.IsRunning(Game, separate);
        if (!fresh)
        {
            var details = new List<string>
            {
                "GPU start flags (GPU block list off, GPU rasterization, high-performance GPU, render size) only work when the browser starts.",
                "ConstiBoost closes the browser windows normally (like clicking X – nothing is force-killed).",
                "The browser then reopens with your previous tabs (--restore-last-session) and the game in a new window.",
                "Save anything you are typing in the browser first.",
                "Choose 'Cancel' to just open the game in the running browser without the flags.",
            };
            if (DialogService.Confirm($"Restart {Game.Publisher} for full GPU speed?", "The browser is running, so the speed flags cannot be applied right now.", details, "RESTART BROWSER"))
            {
                IsBusy = true;
                try
                {
                    fresh = await BrowserLaunchService.CloseGracefullyAsync(Game, separate);
                }
                finally
                {
                    IsBusy = false;
                }

                if (!fresh)
                {
                    NotificationService.Instance.Show($"{Game.Publisher} is still running in the background (browser settings → System → 'Continue running background apps' OFF helps). The game was opened without the start flags.", NotificationKind.Warning);
                }
            }
        }

        if (LaunchStartsFocus && !IsFocused)
        {
            await FocusAsync();
        }

        Report(BrowserLaunchService.Launch(Game, new BrowserLaunchOptions(url, RenderScale, UnlockFps), fresh));
    }
    public ICommand UninstallCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand FocusCommand { get; }
    public ICommand LoadAppsCommand { get; }
    public ICommand SelectResolutionCommand { get; }
    public ICommand SelectMeasuredPresetCommand => new RelayCommand(p => MeasuredPreset = p as string);
    public ICommand MeasureCommand { get; }
    public ICommand CancelMeasureCommand { get; }

    // ---------- Real FPS measurement ----------
    private System.Threading.CancellationTokenSource? _measureCts;
    private bool _isMeasuring;
    private string _measureText = string.Empty;
    private FpsResult? _lastResult;
    private string _calibrationText = string.Empty;
    private string? _measuredPreset;

    public ObservableCollection<FpsRecord> History { get; } = new();
    public bool IsMeasuring { get => _isMeasuring; private set { if (SetProperty(ref _isMeasuring, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string MeasureText { get => _measureText; private set => SetProperty(ref _measureText, value); }
    public string CalibrationText { get => _calibrationText; private set => SetProperty(ref _calibrationText, value); }
    public bool HasResult => _lastResult != null;
    public string ResultAvg => _lastResult is null ? "–" : $"{_lastResult.AverageFps:0}";
    public string ResultLow1 => _lastResult is null ? "–" : $"{_lastResult.Low1:0}";
    public string ResultLow01 => _lastResult is null ? "–" : $"{_lastResult.Low01:0}";
    public string ResultStutters => _lastResult is null ? "–" : $"{_lastResult.Stutters}";
    public string ResultFrameTime => _lastResult is null ? "–" : $"{_lastResult.MedianFrameTimeMs:0.0} ms";
    public IReadOnlyList<double> ResultChart => _lastResult?.PerSecond ?? new List<double>();
    public double ResultChartMax => Math.Max(30, (_lastResult?.MaxFps ?? 60) * 1.15);
    public List<string> PresetNames => _profile?.Presets.Select(p => p.Name).ToList() ?? new List<string> { "Low", "Medium", "High", "Ultra / Epic" };
    public string? MeasuredPreset { get => _measuredPreset; set => SetProperty(ref _measuredPreset, value); }

    public string CompareText
    {
        get
        {
            if (History.Count < 2)
            {
                return History.Count == 1 ? "Measure again after a change to see Before / After." : string.Empty;
            }

            var after = History[0];
            var before = History[1];
            double diff = after.AverageFps - before.AverageFps;
            double pct = before.AverageFps > 0 ? diff / before.AverageFps * 100 : 0;
            string verdict = Math.Abs(pct) < 3 ? "No measurable difference (within normal variation)." : diff > 0 ? "Faster than before." : "Slower than before.";
            return $"Before ({before.TimeText}, {before.Setting}, {before.Profile}): {before.AverageFps:0} FPS · 1 % low {before.Low1:0}\n" +
                   $"After ({after.TimeText}, {after.Setting}, {after.Profile}): {after.AverageFps:0} FPS · 1 % low {after.Low1:0}\n" +
                   $"Difference: {diff:+0;-0;0} FPS ({pct:+0.0;-0.0;0} %) – {verdict}";
        }
    }

    // ---------- Game settings editor (Minecraft / Unreal Engine) ----------
    private IGameConfigEditor? _editor;
    private bool _editorCapFps;

    public bool HasEditor => _editor != null;
    public string EditorTitle => _editor?.Title ?? string.Empty;
    public string EditorFile => _editor?.FileInfo ?? string.Empty;
    public ObservableCollection<ConfigValue> EditorValues { get; } = new();
    public IReadOnlyList<ConfigPreset> EditorPresets => _editor?.Presets ?? Array.Empty<ConfigPreset>();
    public bool EditorCapFps { get => _editorCapFps; set => SetProperty(ref _editorCapFps, value); }
    public ICommand ApplyEditorPresetCommand => new AsyncRelayCommand(p => ApplyEditorPresetAsync(p as ConfigPreset));

    private void LoadEditor()
    {
        _editor = GameConfigEditors.For(Game);
        EditorValues.Clear();
        if (_editor != null)
        {
            foreach (var v in _editor.ReadCurrent())
            {
                EditorValues.Add(v);
            }
        }

        foreach (string n in new[] { nameof(HasEditor), nameof(EditorTitle), nameof(EditorFile), nameof(EditorPresets) })
        {
            OnPropertyChanged(n);
        }
    }

    private async Task ApplyEditorPresetAsync(ConfigPreset? preset)
    {
        if (_editor is null || preset is null)
        {
            return;
        }

        bool running = await Task.Run(() =>
        {
            var p = GameFocusService.FindGameProcesses(Game.ProcessRoots);
            bool any = p.Count > 0;
            p.ForEach(x => x.Dispose());
            return any;
        });
        if (running)
        {
            DialogService.Info("Close the game first", $"{Game.Name} saves its settings when it exits and would overwrite the change. Close it completely, then apply again.");
            return;
        }

        if (!DialogService.Confirm($"Apply {preset.Name} to {Game.Name}?", preset.Description, new[] { "The settings file is backed up first (Backups → RESTORE).", _editor.FileInfo }, "APPLY"))
        {
            return;
        }

        var backup = BackupSession.Create($"{Game.Name} settings {preset.Name}");
        var result = await Task.Run(() => _editor.Apply(preset.Name, EditorCapFps ? FpsCap : null, backup));
        Report(result);
        LoadEditor();
        MainViewModel.Instance?.Backups.Reload();
    }

    // ---------- Guided FPS Tuner (Fortnite) ----------
    private int _targetFps;
    private string _tunerAdvice = string.Empty;
    private Func<BackupSession, OperationResult>? _tunerAction;
    private string _tunerActionText = string.Empty;

    public int TargetFps { get => _targetFps; set { if (SetProperty(ref _targetFps, Math.Clamp(value, 30, 240))) UpdateTuner(); } }
    public string TunerAdvice { get => _tunerAdvice; private set => SetProperty(ref _tunerAdvice, value); }
    public string TunerActionText { get => _tunerActionText; private set => SetProperty(ref _tunerActionText, value); }
    public bool HasTunerAction => _tunerAction != null;
    public ICommand ApplyTunerStepCommand => new RelayCommand(ApplyTunerStep);

    private void UpdateTuner()
    {
        _tunerAction = null;
        TunerActionText = string.Empty;
        if (!Game.IsFortnite)
        {
            return;
        }

        var last = FpsHistory.ForGame(Game.Id).FirstOrDefault();
        var cfg = FortniteConfigService.Read();
        if (last is null)
        {
            TunerAdvice = "Step 1: press MEASURE REAL FPS and play a normal match for 60 s. The tuner then tells you exactly what to change next.";
        }
        else if (!cfg.Exists)
        {
            TunerAdvice = "Fortnite's settings file was not found – start Fortnite once.";
        }
        else
        {
            double avg = last.AverageFps;
            double low = last.Low1;
            double res = double.TryParse(cfg.Get("sg.ResolutionQuality"), NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ? r : 100;
            bool anyHigh = new[] { "sg.ViewDistanceQuality", "sg.ShadowQuality", "sg.EffectsQuality", "sg.PostProcessQuality", "sg.TextureQuality", "sg.FoliageQuality", "sg.ShadingQuality", "sg.AntiAliasingQuality", "sg.GlobalIlluminationQuality", "sg.ReflectionQuality" }
                .Any(k => double.TryParse(cfg.Get(k), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0);
            string measured = $"Last measurement ({last.TimeText}): {avg:0} FPS, 1 % low {low:0}. Target: {TargetFps} FPS.";
            if (avg < TargetFps * 0.95)
            {
                if (cfg.RenderingMode != "Performance" && (ScanService.Instance.Last?.Profile.IsLowPowerIntegrated ?? true))
                {
                    TunerAdvice = measured + "\nNext step: switch Rendering Mode to PERFORMANCE in Fortnite (Settings > Video > Advanced Graphics) and restart the game – the biggest single gain on integrated graphics. Then measure again.";
                }
                else if (anyHigh)
                {
                    TunerAdvice = measured + "\nNext step: set all graphics groups to Low (MAX FPS preset). Then play and measure again.";
                    TunerActionText = "APPLY: all graphics Low";
                    _tunerAction = b => FortniteConfigService.Apply(FortnitePreset.All[0], new PresetOptions((int)res, true, true, null, false), b).result;
                }
                else if (res > 65)
                {
                    int next = (int)Math.Max(60, Math.Round((res - 10) / 5) * 5);
                    TunerAdvice = measured + $"\nNext step: lower 3D resolution from {res:0} % to {next} % (sharper image loss is small, GPU load drops a lot). Then measure again.";
                    TunerActionText = $"APPLY: 3D resolution {next} %";
                    _tunerAction = b => FortniteConfigService.SetValues(new Dictionary<string, string> { ["sg.ResolutionQuality"] = next.ToString("0.000000", CultureInfo.InvariantCulture) }, b, "FPS Tuner");
                }
                else
                {
                    TunerAdvice = measured + "\nYour PC is at its limit for this target with these settings. Options: lower the target, use 1280×720 in-game, close background apps (Game Focus), keep the laptop plugged in and check the Thermal page for heat throttling.";
                }
            }
            else if (avg >= TargetFps * 1.15 && low >= TargetFps * 0.75 && res < 100)
            {
                int next = (int)Math.Min(100, Math.Round((res + 10) / 5) * 5);
                TunerAdvice = measured + $"\nYou have headroom ✓ – you can raise 3D resolution to {next} % for a sharper image, or cap FPS at {TargetFps} for cooler, smoother play.";
                TunerActionText = $"APPLY: 3D resolution {next} %";
                _tunerAction = b => FortniteConfigService.SetValues(new Dictionary<string, string> { ["sg.ResolutionQuality"] = next.ToString("0.000000", CultureInfo.InvariantCulture) }, b, "FPS Tuner");
            }
            else
            {
                TunerAdvice = measured + $"\nTarget reached ✓. Tip: cap FPS at {TargetFps} – more even frame times and less heat.";
                TunerActionText = $"APPLY: FPS limit {TargetFps}";
                _tunerAction = b => FortniteConfigService.SetValues(new Dictionary<string, string> { ["FrameRateLimit"] = TargetFps.ToString("0.000000", CultureInfo.InvariantCulture) }, b, "FPS Tuner");
            }
        }

        OnPropertyChanged(nameof(HasTunerAction));
    }

    private void ApplyTunerStep()
    {
        if (_tunerAction is null)
        {
            return;
        }

        if (!DialogService.Confirm("Apply tuner step?", TunerActionText.Replace("APPLY: ", string.Empty), new[] { "Fortnite must be closed. GameUserSettings.ini is backed up first.", "Afterwards: start Fortnite, play the same situation and press MEASURE REAL FPS again." }, "APPLY"))
        {
            return;
        }

        var result = _tunerAction(BackupSession.Create("Fortnite FPS Tuner"));
        Report(result);
        ReadFortniteSettings();
        UpdateEstimates();
        UpdateTuner();
        MainViewModel.Instance?.Backups.Reload();
    }

    private void LoadHistory()
    {
        History.Clear();
        foreach (var r in FpsHistory.ForGame(Game.Id).Take(12))
        {
            History.Add(r);
        }

        OnPropertyChanged(nameof(CompareText));
    }

    private async Task MeasureAsync(int seconds)
    {
        if (!DialogService.Confirm($"Measure real FPS in {Game.Name}?",
                $"ConstiBoost records the frames Windows reports for the game (DirectX present events – no injection, anti-cheat safe). Windows only allows this with administrator rights, so a UAC prompt appears once.\n\nStart the game, then play normally for {seconds} seconds – measuring starts automatically as soon as the game renders.",
                new[] { "Tip: measure the same situation each time (e.g. a Creative map or the same match phase) for a fair Before / After." }, "START MEASURING"))
        {
            return;
        }

        IsMeasuring = true;
        _measureCts = new System.Threading.CancellationTokenSource();
        var roots = Game.ProcessRoots.Select(r => GameLibraryService.NormalizePath(r) + "\\").ToList();
        var exeNames = new HashSet<string>(Game.ExecutableNames, StringComparer.OrdinalIgnoreCase);
        var temps = new List<double>();
        var clocks = new List<double>();
        var sensorTask = Task.Run(async () =>
        {
            using var freq = new Hardware.CpuFrequencySampler(ScanService.Instance.Last?.Hardware.CpuMaxClockMhz);
            freq.Sample();
            while (!_measureCts.IsCancellationRequested)
            {
                await Task.Delay(2000);
                if (Hardware.Sensors.SensorHub.Instance.Read().BestTemperature is double t) temps.Add(t);
                if (freq.Sample() is double mhz) clocks.Add(mhz);
            }
        });

        try
        {
            var result = await FpsCaptureService.Instance.MeasureAsync(seconds,
                (path, name) => path != null ? roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)) : exeNames.Contains(name),
                new Progress<string>(t => MeasureText = t), _measureCts.Token);
            if (result is null)
            {
                MeasureText = FpsCaptureService.Instance.LastError ?? "Measurement not possible.";
                NotificationService.Instance.Show(MeasureText, NotificationKind.Warning);
                return;
            }

            _lastResult = result;
            var profile = GamingModeService.Instance.ActiveProfile;
            FpsHistory.Add(new FpsRecord
            {
                GameId = Game.Id,
                GameName = Game.Name,
                Time = DateTime.Now,
                Profile = profile is null ? "Normal" : GamingModeService.ProfileName(profile.Value),
                Setting = MeasuredPreset ?? "Unknown",
                Resolution = _resolution.Label,
                RenderScale = _scale,
                AverageFps = result.AverageFps,
                Low1 = result.Low1,
                Low01 = result.Low01,
                Stutters = result.Stutters,
                Seconds = result.Seconds,
                AverageTemperature = temps.Count > 0 ? temps.Average() : null,
                MaxTemperature = temps.Count > 0 ? temps.Max() : null,
                AverageCpuMhz = clocks.Count > 0 ? clocks.Average() : null,
            });
            ActionLog.Success($"FPS measured: {Game.Name}", null, result.Summary);
            MeasureText = $"Measured {result.ProcessName} for {result.Seconds:0} s: {result.Summary}";
            foreach (string n in new[] { nameof(HasResult), nameof(ResultAvg), nameof(ResultLow1), nameof(ResultLow01), nameof(ResultStutters), nameof(ResultFrameTime), nameof(ResultChart), nameof(ResultChartMax) })
            {
                OnPropertyChanged(n);
            }

            LoadHistory();
            UpdateEstimates();
            UpdateTuner();
            MainViewModel.Instance?.Thermal.RefreshComparison();
        }
        catch (OperationCanceledException)
        {
            MeasureText = "Measurement cancelled.";
        }
        finally
        {
            _measureCts.Cancel();
            IsMeasuring = false;
        }
    }

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

    public GameAutoProfile? AutoProfile { get; private set; }

    /// <summary>Saves the current Focus options as this game's auto profile.</summary>
    public bool AutoFocusEnabled
    {
        get => AutoProfile?.Enabled == true;
        set
        {
            AutoProfile = new GameAutoProfile
            {
                GameId = Game.Id,
                GameName = Game.Name,
                InstallPath = Game.InstallPath,
                ProcessRoots = Game.ProcessRoots.ToList(),
                MainExecutable = Game.MainExecutable,
                EpicAppName = Game.EpicAppName,
                Enabled = value,
                Profile = _focusProfile,
                BoostPriority = BoostPriority,
                GpuPreference = GpuPreference,
                MuteNotifications = MuteNotifications,
                LowerPriority = LowerPriority,
                CapFps = CapFps && Game.IsFortnite,
                FpsCap = FpsCap,
                CloseApps = Apps.Where(a => a.IsSelected).Select(a => a.ProcessName).ToList(),
            };
            AutoProfileService.Instance.Save(AutoProfile);
            OnPropertyChanged();
            NotificationService.Instance.Show(value
                ? $"Auto-Focus for {Game.Name} is on: when the game starts, ConstiBoost applies these options automatically (ConstiBoost must be running, e.g. in the tray)."
                : $"Auto-Focus for {Game.Name} is off.", NotificationKind.Info);
        }
    }

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

        MeasuredPreset ??= _fortnitePreset ?? _profile?.Presets.FirstOrDefault()?.Name;
        if (_targetFps == 0)
        {
            _targetFps = FpsCap;
            OnPropertyChanged(nameof(TargetFps));
        }

        LoadHistory();
        LoadEditor();
        UpdateTuner();
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

        // Calibration: compare the latest real measurement with the estimate for the same setting.
        double factor = 1.0;
        CalibrationText = string.Empty;
        var reference = FpsHistory.ForGame(Game.Id).FirstOrDefault(r => _profile.Presets.Any(p => p.Name == r.Setting));
        if (reference != null)
        {
            var refPreset = _profile.Presets.First(p => p.Name == reference.Setting);
            var refRes = Resolutions.FirstOrDefault(r => r.Label == reference.Resolution) ?? _resolution;
            var refEstimate = FpsEstimator.Estimate(_profile, refPreset, refRes.Width, refRes.Height, reference.RenderScale / 100.0, _context);
            if (refEstimate != null)
            {
                double mid = (refEstimate.Low + refEstimate.High) / 2.0;
                factor = Math.Clamp(reference.AverageFps / Math.Max(1, mid), 0.4, 2.5);
                CalibrationText = $"✓ Calibrated with your real measurement from {reference.TimeText} ({reference.AverageFps:0} FPS at {reference.Setting}) – correction ×{factor:0.00}.";
            }
        }

        foreach (var preset in _profile.Presets)
        {
            var e = FpsEstimator.Estimate(_profile, preset, _resolution.Width, _resolution.Height, _scale / 100.0, _context, preset.Name == _fortnitePreset);
            if (e != null)
            {
                Estimates.Add(Math.Abs(factor - 1.0) < 0.001 ? e : e with { Low = (int)Math.Round(e.Low * factor), High = (int)Math.Round(e.High * factor) });
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
            g.IsSelected = AutoProfile is { } ap
                ? ap.CloseApps.Contains(g.ProcessName, StringComparer.OrdinalIgnoreCase)
                : g.IsKnownBackgroundApp && !launcherForThisGame && !BackgroundAppsService.KeepPriority.Contains(g.ProcessName) &&
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
            if (SettingsService.Instance.Current.FocusDisableThrottling) details.Add("No Windows efficiency mode (EcoQoS) for the game processes");
            if (Game.IsBrowser && SettingsService.Instance.Current.FocusLowerOtherTabs) details.Add("Other browser tabs: priority 'Below normal' (extensions untouched)");
            if (SettingsService.Instance.Current.FocusPauseSearch) details.Add("Pause Windows Search indexing (UAC prompt; restarted on Stop)");
            if (Game.IsBrowser) details.Add("Browser: priority 'Above normal' only for the browser GPU process and the game tab (busiest tab) – other tabs stay normal");
            if (MuteNotifications) details.Add("Mute Windows notification banners");
            if (CapFps && Game.IsFortnite) details.Add($"Fortnite frame rate limit {FpsCap} FPS (only while Fortnite is closed)");
            details.Add("Everything is restored when you press STOP FOCUS.");
            if (!DialogService.Confirm($"Focus on {Game.Name}?", "ConstiBoost puts all safe system resources behind this game:", details, "START FOCUS"))
            {
                return;
            }

            var messages = await GameFocusService.Instance.StartAsync(Game, new FocusOptions(_focusProfile, apps, LowerPriority, BoostPriority, GpuPreference, MuteNotifications, CapFps ? FpsCap : null, Game.IsBrowser,
                SettingsService.Instance.Current.FocusDisableThrottling, SettingsService.Instance.Current.FocusLowerOtherTabs, SettingsService.Instance.Current.FocusPauseSearch));
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
