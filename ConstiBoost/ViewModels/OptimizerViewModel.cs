using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Optimizers;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public enum OptimizerPhase
{
    Idle,
    Analyzing,
    Review,
    Applying,
    Done,
}

/// <summary>
/// One-click flow: 1. analyze → 2. show → 3. confirm → 4. backup → 5. apply → 6. result.
/// Also the full Windows optimizer list with current/recommended state, reason and downsides.
/// </summary>
public sealed class OptimizerViewModel : PageViewModel
{
    private OptimizerPhase _phase;
    private OptimizationContext _context = new();
    private string _summary = string.Empty;
    private int? _scoreBefore;
    private int? _scoreAfter;
    private string _progressText = string.Empty;
    private bool _analyzedOnce;

    public OptimizerViewModel()
    {
        Optimizations = new ObservableCollection<Optimization>(OptimizationCatalog.CreateAll());
        AnalyzeCommand = new AsyncRelayCommand(() => AnalyzeAsync(true), () => !IsBusy);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => !IsBusy && Optimizations.Any(o => o.IsSelected && o.IsAvailable));
        SelectRecommendedCommand = new RelayCommand(SelectRecommended);
        OpenGameBarSettingsCommand = new RelayCommand(() => OpenUri("ms-settings:gaming-gamebar"));
        OpenGraphicsSettingsCommand = new RelayCommand(() => OpenUri("ms-settings:display-advancedgraphics"));
    }

    public override string Key => "optimizer";
    public override string Title => "Optimizer";
    public override string Icon => "";

    public ObservableCollection<Optimization> Optimizations { get; }
    public ObservableCollection<Optimization> Found { get; } = new();
    public ObservableCollection<ServiceStatus> GamingServices { get; } = new();
    public ObservableCollection<string> Results { get; } = new();

    public ICommand AnalyzeCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand SelectRecommendedCommand { get; }
    public ICommand OpenGameBarSettingsCommand { get; }
    public ICommand OpenGraphicsSettingsCommand { get; }

    public OptimizerPhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsReview));
                OnPropertyChanged(nameof(IsDone));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsBusy => Phase is OptimizerPhase.Analyzing or OptimizerPhase.Applying;
    public bool IsReview => Phase == OptimizerPhase.Review;
    public bool IsDone => Phase == OptimizerPhase.Done;

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    public string ScoreChangeText => _scoreBefore.HasValue && _scoreAfter.HasValue
        ? $"Gaming Readiness: {_scoreBefore}/100 → {_scoreAfter}/100"
        : string.Empty;

    public string GameModeState => GameFeatures.GameModeEnabled ? "ON" : "OFF";
    public string GameBarState => GameFeatures.GameDvrEnabled ? "ON (captures enabled)" : "Captures OFF";
    public string GameBarControllerState => GameFeatures.GameBarControllerEnabled ? "ON" : "OFF";
    public string HagsState => GameFeatures.HagsEnabled switch { true => "ON", false => "OFF", _ => "Unavailable" };

    private bool _pendingOneClick;

    /// <summary>Entry point of "⚡ OPTIMIZE MY PC": opens this page and runs the analyze → review flow.</summary>
    public void StartOneClick()
    {
        var main = MainViewModel.Instance;
        if (main is null || ReferenceEquals(main.CurrentPage, this))
        {
            _ = AnalyzeAsync(true);
            return;
        }

        _pendingOneClick = true;
        main.Navigate(Key);
    }

    public override void OnNavigatedTo()
    {
        if (_pendingOneClick)
        {
            _pendingOneClick = false;
            _ = AnalyzeAsync(true);
        }
        else if (!_analyzedOnce && !IsBusy)
        {
            _ = AnalyzeAsync(false);
        }
    }

    public async Task AnalyzeAsync(bool oneClick)
    {
        if (IsBusy)
        {
            return;
        }

        Phase = OptimizerPhase.Analyzing;
        Results.Clear();
        ProgressText = "Analyzing your system…";
        try
        {
            var scan = ScanService.Instance.Last ?? await ScanService.Instance.ScanAsync();
            _context = new OptimizationContext { Hardware = scan.Hardware, Profile = scan.Profile };
            await Task.Run(() =>
            {
                foreach (var o in Optimizations)
                {
                    o.Refresh(_context);
                }
            });
            await Task.Run(LoadGamingServices);

            Found.Clear();
            foreach (var o in Optimizations)
            {
                o.IsSelected = o.IsAvailable && o.NeedsChange && !o.IsUserChoice && o.PreselectWhenNeeded(_context);
                if (o.IsAvailable && o.NeedsChange && !o.IsUserChoice)
                {
                    Found.Add(o);
                }
            }

            Summary = Found.Count == 0
                ? "Your system is already well configured. Nothing important to change."
                : $"Optimization found: {Found.Count} item(s). Recommended items are pre-selected – review them below.";
            _analyzedOnce = true;
            Phase = oneClick && Found.Count > 0 ? OptimizerPhase.Review : OptimizerPhase.Idle;
            if (oneClick && Found.Count == 0)
            {
                NotificationService.Instance.Show("No optimizations needed – your PC is already in good shape.", NotificationKind.Success);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Analyze failed", ex);
            Summary = "The analysis couldn't be completed. Details were written to the log.";
            Phase = OptimizerPhase.Idle;
        }

        OnPropertyChanged(nameof(GameModeState));
        OnPropertyChanged(nameof(GameBarState));
        OnPropertyChanged(nameof(GameBarControllerState));
        OnPropertyChanged(nameof(HagsState));
    }

    private void SelectRecommended()
    {
        foreach (var o in Optimizations)
        {
            o.IsSelected = o.IsAvailable && o.NeedsChange && !o.IsUserChoice && o.PreselectWhenNeeded(_context);
        }
    }

    private async Task ApplyAsync()
    {
        var selected = Optimizations.Where(o => o.IsSelected && o.IsAvailable).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var details = selected.Select(o =>
            $"{o.Name}: {o.CurrentState} → {o.RecommendedState}" +
            (o.RequiresAdmin ? "  [administrator permission]" : string.Empty) +
            (o.IsReversible ? string.Empty : "  [deletes temp/cache files]")).ToList();
        details.Add("A backup of every changed setting is created first.");
        if (!DialogService.Confirm("Apply optimizations?", "ConstiBoost will make the following changes:", details, "APPLY OPTIMIZATIONS"))
        {
            return;
        }

        Phase = OptimizerPhase.Applying;
        Results.Clear();
        _scoreBefore = ScanService.Instance.Last?.Score;
        try
        {
            bool systemChanges = selected.Any(o => o.IsReversible);
            if (SettingsService.Instance.Current.CreateRestorePointBeforeMajorChanges && systemChanges && (selected.Count >= 3 || selected.Any(o => o.RequiresAdmin)))
            {
                ProgressText = "Creating Windows restore point (UAC)…";
                var rp = await RestorePointService.CreateAsync("ConstiBoost – before optimization");
                if (!rp.Success && !DialogService.Confirm("Restore point not created", rp.Message + "\n\nContinue with the ConstiBoost backup only?", null, "CONTINUE"))
                {
                    Phase = OptimizerPhase.Review;
                    return;
                }
            }

            ProgressText = "Creating backup…";
            var backup = BackupSession.Create("Optimize my PC");
            int ok = 0;
            foreach (var o in selected)
            {
                ProgressText = $"Applying: {o.Name}…";
                var result = await o.ApplyAsync(backup, _context);
                if (result.Success)
                {
                    ok++;
                    Results.Add($"✓ {o.Name} – {result.Message}");
                }
                else
                {
                    Results.Add($"✕ {o.Name} – {result.Message}" + (result.TechnicalDetails is null ? string.Empty : $" ({result.TechnicalDetails})"));
                }

                o.IsSelected = false;
            }

            ProgressText = "Re-checking Gaming Readiness…";
            var scan = await ScanService.Instance.ScanAsync();
            _scoreAfter = scan.Score;
            Summary = $"{ok} of {selected.Count} optimization(s) applied. Backup: {backup.Manifest.Id}";
            NotificationService.Instance.Show(Summary, ok == selected.Count ? NotificationKind.Success : NotificationKind.Warning);
            OnPropertyChanged(nameof(ScoreChangeText));

            Found.Clear();
            foreach (var o in Optimizations.Where(o => o.IsAvailable && o.NeedsChange && !o.IsUserChoice))
            {
                Found.Add(o);
            }

            MainViewModel.Instance?.Backups.Reload();
        }
        catch (Exception ex)
        {
            AppLog.Error("Apply failed", ex);
            Results.Add("✕ This optimization couldn't be applied. Details were written to the log.");
        }
        finally
        {
            Phase = OptimizerPhase.Done;
            OnPropertyChanged(nameof(GameModeState));
            OnPropertyChanged(nameof(GameBarState));
            OnPropertyChanged(nameof(HagsState));
        }
    }

    private void LoadGamingServices()
    {
        var services = new (string name, string purpose)[]
        {
            ("GamingServices", "Microsoft Store / Xbox app games"),
            ("GamingServicesNet", "Xbox networking for Store games"),
            ("XblAuthManager", "Xbox Live sign-in"),
            ("XblGameSave", "Xbox cloud saves"),
            ("XboxNetApiSvc", "Xbox Live multiplayer networking"),
            ("XboxGipSvc", "Xbox accessories (controllers)"),
        };
        var list = new List<ServiceStatus>();
        foreach (var (name, purpose) in services)
        {
            try
            {
                using var sc = new ServiceController(name);
                list.Add(new ServiceStatus(sc.DisplayName, sc.Status.ToString(), purpose));
            }
            catch (InvalidOperationException)
            {
                list.Add(new ServiceStatus(name, "Not installed", purpose));
            }
            catch (Exception ex)
            {
                list.Add(new ServiceStatus(name, "Unavailable (" + ex.Message + ")", purpose));
            }
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            GamingServices.Clear();
            foreach (var s in list)
            {
                GamingServices.Add(s);
            }
        });
    }

    private static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            NotificationService.Instance.Show("Windows settings couldn't be opened: " + ex.Message, NotificationKind.Error);
        }
    }
}
