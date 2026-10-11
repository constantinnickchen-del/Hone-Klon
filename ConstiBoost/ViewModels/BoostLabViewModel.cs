using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Optimizers;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

/// <summary>
/// 🧪 Boost Lab – for low-end PCs that want more performance: hardware insights (what really limits FPS)
/// and the Low-End Boost Pack (all safe, reversible Windows settings in one place).
/// </summary>
public sealed class BoostLabViewModel : PageViewModel
{
    private bool _isBusy;
    private bool _loaded;
    private string _progress = string.Empty;
    private OptimizationContext _context = new();

    public BoostLabViewModel()
    {
        Pack = new ObservableCollection<Optimization>(new Optimization[]
        {
            new PowerOptimization(), new GameModeOptimization(), new BackgroundRecordingOptimization(), new GameDvrOptimization(),
            new StartupAppsOptimization(), new TransparencyOptimization(), new AnimationsOptimization(), new DesktopEffectsOptimization(),
            new SuggestionsOptimization(), new BackgroundAppsOptimization(), new TempFilesOptimization(),
        });
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => !IsBusy && Pack.Any(p => p.IsSelected && p.IsAvailable));
        OpenGamesCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("games"));
        OpenThermalCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("thermal"));
    }

    public override string Key => "boostlab";
    public override string Title => "Boost Lab";
    public override string Icon => "";

    public ObservableCollection<HardwareInsight> Insights { get; } = new();
    public ObservableCollection<Optimization> Pack { get; }
    public ObservableCollection<string> Results { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand OpenGamesCommand { get; }
    public ICommand OpenThermalCommand { get; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string ProgressText { get => _progress; private set => SetProperty(ref _progress, value); }

    public int BottleneckCount => Insights.Count(i => i.Status is CheckStatus.Bad or CheckStatus.Warning);
    public string HeadlineText => !_loaded ? "Analyzing what limits your FPS…"
        : BottleneckCount == 0 ? "No major bottlenecks found – your PC is configured well for its hardware."
        : $"{BottleneckCount} thing(s) found that can hold your FPS back.";
    public string ProfileText => ScanService.Instance.Last?.Profile.Description ?? string.Empty;

    public override void OnNavigatedTo()
    {
        if (!_loaded && !IsBusy)
        {
            _ = LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        ProgressText = "Reading hardware…";
        try
        {
            var scan = ScanService.Instance.Last ?? await ScanService.Instance.ScanAsync();
            _context = new OptimizationContext { Hardware = scan.Hardware, Profile = scan.Profile };
            var gameDrives = MainViewModel.Instance?.Games.Games.Select(g => Path.GetPathRoot(g.InstallPath) ?? string.Empty).Where(d => d.Length > 0).Distinct().ToList() ?? new List<string>();
            ProgressText = "Checking memory, drivers, storage and display…";
            var insights = await Task.Run(() => HardwareInsightsService.Analyze(scan.Hardware, gameDrives));
            Insights.Clear();
            foreach (var i in insights)
            {
                Insights.Add(i);
            }

            ProgressText = "Checking Windows settings…";
            await Task.Run(() =>
            {
                foreach (var o in Pack)
                {
                    o.Refresh(_context);
                }
            });
            foreach (var o in Pack)
            {
                o.IsSelected = o.IsAvailable && o.NeedsChange && !o.IsUserChoice && o.PreselectWhenNeeded(_context);
            }

            _loaded = true;
            OnPropertyChanged(nameof(BottleneckCount));
            OnPropertyChanged(nameof(HeadlineText));
            OnPropertyChanged(nameof(ProfileText));
        }
        catch (Exception ex)
        {
            AppLog.Error("Boost Lab load failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAsync()
    {
        var selected = Pack.Where(p => p.IsSelected && p.IsAvailable).ToList();
        var details = selected.Select(o => $"{o.Name}: {o.CurrentState} → {o.RecommendedState}").ToList();
        details.Add("A backup of every changed setting is created first (Backups → RESTORE).");
        if (!DialogService.Confirm("Apply the Low-End Boost Pack?", "These safe Windows changes will be made:", details, "APPLY BOOST PACK"))
        {
            return;
        }

        IsBusy = true;
        Results.Clear();
        try
        {
            var backup = BackupSession.Create("Low-End Boost Pack");
            foreach (var o in selected)
            {
                ProgressText = $"Applying: {o.Name}…";
                var r = await o.ApplyAsync(backup, _context);
                Results.Add((r.Success ? "✓ " : "✕ ") + $"{o.Name} – {r.Message}");
                o.IsSelected = false;
            }

            await ScanService.Instance.ScanAsync();
            MainViewModel.Instance?.Backups.Reload();
            NotificationService.Instance.Show("Low-End Boost Pack applied.", NotificationKind.Success);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
