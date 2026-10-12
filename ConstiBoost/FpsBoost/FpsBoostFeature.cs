using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Localization;
using ConstiBoost.Logging;
using ConstiBoost.Models;

namespace ConstiBoost.FpsBoost;

public sealed record BoostAction(string Label, ICommand Command);

/// <summary>Remembers which backup belongs to which applied FPS Boost feature (for per-feature Undo).</summary>
public static class FpsBoostStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, string>? _map;

    private static string FilePath => Path.Combine(AppPaths.Root, "fps-boost.json");

    private static Dictionary<string, string> Map
    {
        get
        {
            if (_map != null)
            {
                return _map;
            }

            try
            {
                _map = File.Exists(FilePath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) : null;
            }
            catch (Exception ex)
            {
                AppLog.Warn("fps-boost.json unreadable: " + ex.Message);
            }

            return _map ??= new Dictionary<string, string>();
        }
    }

    public static string? Get(string id)
    {
        lock (Gate)
        {
            return Map.TryGetValue(id, out string? folder) && Directory.Exists(folder) ? folder : null;
        }
    }

    public static void Set(string id, string? folder)
    {
        lock (Gate)
        {
            if (folder is null)
            {
                Map.Remove(id);
            }
            else
            {
                Map[id] = folder;
            }

            Safe.Try(() => File.WriteAllText(FilePath, JsonSerializer.Serialize(Map)), "Save fps-boost.json");
        }
    }
}

/// <summary>
/// One FPS Boost feature: a real check of the current state, an honest explanation and – where something can be
/// changed – Apply (with backup) and Undo. Features without a safe automatic change only explain and offer actions.
/// </summary>
public abstract class FpsBoostFeature : ObservableObject
{
    private CheckStatus _status = CheckStatus.Unavailable;
    private string _stateText = L.T("Checking…");
    private string _detail = string.Empty;
    private bool _canImprove;
    private bool _isBusy;
    private string? _resultText;
    private IReadOnlyList<BoostAction> _actions = Array.Empty<BoostAction>();

    public int Number { get; set; }
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Icon { get; }
    public abstract string Category { get; }

    /// <summary>"HIGH", "MEDIUM", "LOW" or "INFO" – honest expected effect on FPS.</summary>
    public abstract string Impact { get; }

    public abstract string Description { get; }
    public virtual string Downside => L.T("None.");

    /// <summary>Whether this feature changes a setting (Apply / Undo) or is a check/guide.</summary>
    public virtual bool ChangesSettings => true;

    /// <summary>Included in "Apply all recommended".</summary>
    public virtual bool InApplyAll => ChangesSettings;

    public CheckStatus Status { get => _status; protected set => SetProperty(ref _status, value); }
    public string StateText { get => _stateText; protected set => SetProperty(ref _stateText, value); }
    public string Detail { get => _detail; protected set => SetProperty(ref _detail, value); }
    public IReadOnlyList<BoostAction> Actions { get => _actions; protected set => SetProperty(ref _actions, value); }
    public string? ResultText { get => _resultText; protected set => SetProperty(ref _resultText, value); }

    public bool CanImprove
    {
        get => _canImprove;
        protected set
        {
            if (SetProperty(ref _canImprove, value))
            {
                OnPropertyChanged(nameof(ShowApply));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowApply));
                OnPropertyChanged(nameof(ShowUndo));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasBackup => FpsBoostStore.Get(Id) != null;
    public bool ShowApply => ChangesSettings && CanImprove && !IsBusy;
    public bool ShowUndo => ChangesSettings && HasBackup && !IsBusy;

    public ICommand ApplyCommand => new AsyncRelayCommand(async () => await ApplyAsync());
    public ICommand UndoCommand => new AsyncRelayCommand(async () => await UndoAsync());

    /// <summary>Reads the real current state. Runs on a background thread.</summary>
    public void Check()
    {
        try
        {
            CheckCore();
        }
        catch (Exception ex)
        {
            Unavailable(L.T("This optimization is unavailable on your system.") + " " + Safe.Describe(ex));
            AppLog.Warn($"FPS Boost check {Id} failed: {ex.Message}");
        }

        OnPropertyChanged(nameof(HasBackup));
        OnPropertyChanged(nameof(ShowUndo));
        OnPropertyChanged(nameof(ShowApply));
    }

    protected abstract void CheckCore();

    protected virtual Task<OperationResult> ApplyCoreAsync(BackupSession backup) =>
        Task.FromResult(OperationResult.Unavailable(L.T("Nothing to apply.")));

    public async Task<OperationResult> ApplyAsync()
    {
        if (!CanImprove || IsBusy)
        {
            return OperationResult.Ok(L.T("Already optimal."));
        }

        IsBusy = true;
        OperationResult result;
        BackupSession? backup = null;
        try
        {
            string? existing = FpsBoostStore.Get(Id);
            backup = existing != null ? BackupSession.Load(existing) : BackupSession.Create("FPS Boost: " + Name);
            result = await ApplyCoreAsync(backup);
            if (result.Success && !backup.IsEmpty)
            {
                FpsBoostStore.Set(Id, backup.Folder);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"FPS Boost apply {Id} failed", ex);
            result = OperationResult.Fail("This optimization couldn't be applied.", Safe.Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }

        ResultText = result.Success ? "✓ " + L.T(result.Message) : "✗ " + L.T(result.Message) + (result.TechnicalDetails is null ? string.Empty : $" ({result.TechnicalDetails})");
        await Task.Run(Check);
        return result;
    }

    public async Task<OperationResult> UndoAsync()
    {
        string? folder = FpsBoostStore.Get(Id);
        if (folder is null || IsBusy)
        {
            return OperationResult.Ok(L.T("Nothing to undo."));
        }

        IsBusy = true;
        try
        {
            var session = BackupSession.Load(folder);
            var report = await BackupService.Instance.RestoreAsync(session);
            await OnUndoneAsync();
            if (report.Failed == 0)
            {
                FpsBoostStore.Set(Id, null);
                ResultText = L.T("↩ Restored the previous state.");
            }
            else
            {
                ResultText = L.T("Restore finished with notes: ") + string.Join("; ", report.Messages);
            }
        }
        catch (Exception ex)
        {
            ResultText = L.T("Undo failed: ") + Safe.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await Task.Run(Check);
        return OperationResult.Ok(ResultText ?? string.Empty);
    }

    protected virtual Task OnUndoneAsync() => Task.CompletedTask;

    protected void Set(CheckStatus status, string state, string detail, bool canImprove)
    {
        Status = status;
        StateText = L.T(state);
        Detail = L.T(detail);
        CanImprove = canImprove;
    }

    protected void Unavailable(string reason) => Set(CheckStatus.Unavailable, "Unavailable", reason, false);

    protected void ShowResult(OperationResult r) =>
        ResultText = r.Success ? "✓ " + L.T(r.Message) : "✗ " + L.T(r.Message) + (r.TechnicalDetails is null ? string.Empty : $" ({r.TechnicalDetails})");
}
