using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.FpsBoost;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed record BoostGroup(string Name, IReadOnlyList<FpsBoostFeature> Features);

/// <summary>⚡ FPS Boost – 20 real, checked and reversible FPS features in one place.</summary>
public sealed class FpsBoostViewModel : PageViewModel
{
    private bool _isBusy;
    private bool _loaded;
    private string _summary = "Checking your system…";

    public FpsBoostViewModel()
    {
        Features = FpsBoostCatalog.Create();
        Groups = Features.GroupBy(f => f.Category).Select(g => new BoostGroup(g.Key, g.ToList())).ToList();
        CheckAllCommand = new AsyncRelayCommand(CheckAllAsync, () => !IsBusy);
        ApplyAllCommand = new AsyncRelayCommand(ApplyAllAsync, () => !IsBusy);
        OpenGamesCommand = new RelayCommand(() => MainViewModel.Instance?.Navigate("games"));
        foreach (var f in Features)
        {
            f.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(FpsBoostFeature.Status) or nameof(FpsBoostFeature.CanImprove))
                {
                    UpdateSummary();
                }
            };
        }
    }

    public override string Key => "fpsboost";
    public override string Title => "FPS Boost";
    public override string Icon => "";

    public IReadOnlyList<FpsBoostFeature> Features { get; }
    public IReadOnlyList<BoostGroup> Groups { get; }
    public ICommand CheckAllCommand { get; }
    public ICommand ApplyAllCommand { get; }
    public ICommand OpenGamesCommand { get; }

    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public override void OnNavigatedTo()
    {
        if (!_loaded && !IsBusy)
        {
            _ = CheckAllAsync();
        }
    }

    private async Task CheckAllAsync()
    {
        IsBusy = true;
        Summary = "Checking your system…";
        try
        {
            if (ScanService.Instance.Last is null)
            {
                await ScanService.Instance.ScanAsync();
            }

            await Task.WhenAll(Features.Select(f => Task.Run(f.Check)));
            _loaded = true;
        }
        finally
        {
            IsBusy = false;
            UpdateSummary();
        }
    }

    private void UpdateSummary()
    {
        if (!_loaded)
        {
            return;
        }

        int good = Features.Count(f => f.Status == CheckStatus.Good);
        int improvable = Features.Count(f => f.ShowApply);
        int hints = Features.Count(f => f.Status == CheckStatus.Warning && !f.ChangesSettings);
        int unavailable = Features.Count(f => f.Status == CheckStatus.Unavailable);
        Summary = $"{good} of {Features.Count} optimal · {improvable} can be applied · {hints} hint(s) to check · {unavailable} not available on this PC";
    }

    private async Task ApplyAllAsync()
    {
        var todo = Features.Where(f => f.InApplyAll && f.ShowApply).ToList();
        if (todo.Count == 0)
        {
            NotificationService.Instance.Show("Everything that can be applied automatically is already optimal.", NotificationKind.Success);
            return;
        }

        var details = todo.Select(f => $"{f.Icon} {f.Name} – {f.Detail}").ToList();
        details.Add("Every change gets a backup and can be undone per feature (UNDO) or in Backups.");
        details.Add("Not included (your choice, apply them individually): gaming resolution, refresh rate, update sharing.");
        if (!DialogService.Confirm($"Apply {todo.Count} FPS boosts?", "ConstiBoost will change these settings:", details, "APPLY ALL"))
        {
            return;
        }

        IsBusy = true;
        int ok = 0;
        var failed = new List<string>();
        try
        {
            foreach (var f in todo)
            {
                var r = await f.ApplyAsync();
                if (r.Success)
                {
                    ok++;
                }
                else
                {
                    failed.Add($"{f.Name}: {r.Message}");
                }
            }
        }
        finally
        {
            IsBusy = false;
            UpdateSummary();
        }

        NotificationService.Instance.Show(failed.Count == 0 ? $"⚡ {ok} FPS boosts applied." : $"{ok} applied, {failed.Count} not: " + string.Join("; ", failed),
            failed.Count == 0 ? NotificationKind.Success : NotificationKind.Warning);
    }
}
