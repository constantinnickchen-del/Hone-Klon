using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Localization;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.ViewModels;

public sealed class ProcessesViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ProcessSampler _sampler = new();
    private readonly Dictionary<int, ProcessRow> _rows = new();
    private bool _sampling;
    private ProcessRow? _selected;
    private string _summary = string.Empty;

    public ProcessesViewModel()
    {
        _timer.Tick += async (_, _) => await RefreshAsync();
        CloseCommand = new AsyncRelayCommand(CloseSelectedAsync, () => Selected is { CanClose: true });
    }

    public ObservableCollection<ProcessRow> Rows { get; } = new();

    public ICommand CloseCommand { get; }

    public ProcessRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _ = RefreshAsync();
            _timer.Start();
        }
    }

    public void Stop() => _timer.Stop();

    private async Task RefreshAsync()
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;
        try
        {
            var samples = await Task.Run(_sampler.Sample);
            var seen = new HashSet<int>();
            foreach (var s in samples)
            {
                seen.Add(s.Pid);
                if (!_rows.TryGetValue(s.Pid, out var row))
                {
                    row = new ProcessRow { Pid = s.Pid, Name = s.Name, Path = s.Path, Protection = s.Protection };
                    _rows[s.Pid] = row;
                    Rows.Add(row);
                }

                row.Cpu = s.Cpu;
                row.Ram = s.Ram;
                row.Gpu = s.Gpu;
                row.Status = s.Status;
            }

            foreach (int pid in _rows.Keys.Where(p => !seen.Contains(p)).ToList())
            {
                Rows.Remove(_rows[pid]);
                _rows.Remove(pid);
            }

            CollectionViewSource.GetDefaultView(Rows)?.Refresh();
            Summary = L.F("{0} processes · updates every 2 s while this tab is open", Rows.Count);
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Process refresh failed: " + ex.Message);
        }
        finally
        {
            _sampling = false;
        }
    }

    private async Task CloseSelectedAsync()
    {
        var row = Selected;
        if (row is null)
        {
            return;
        }

        if (row.Protection == ProcessProtection.Critical)
        {
            DialogService.Info("Protected process", L.F("{0} is a critical Windows or security process. ConstiBoost never closes it.", row.Name));
            return;
        }

        string message = row.Protection == ProcessProtection.Caution
            ? L.F("{0} is an important app (e.g. explorer.exe runs the taskbar and desktop). Closing it can make the taskbar disappear until it restarts. Continue?", row.Name)
            : L.F("Close {0} (PID {1})? Unsaved work in this app may be lost.", row.Name, row.Pid);
        if (!DialogService.Confirm("Close process?", message, null, "CLOSE PROCESS", danger: true))
        {
            return;
        }

        var result = await ProcessService.CloseAsync(row.Pid, row.Name, allowKill: true);
        NotificationService.Instance.Show(result.Message, result.Success ? NotificationKind.Success : NotificationKind.Warning);
        await RefreshAsync();
    }
}
