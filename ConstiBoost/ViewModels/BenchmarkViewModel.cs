using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;

namespace ConstiBoost.ViewModels;

public sealed record ComparisonRow(string Metric, string Before, string After, string Difference, bool? Better);

/// <summary>"System Performance Benchmark" – explicitly not a Fortnite FPS benchmark.</summary>
public sealed class BenchmarkViewModel : ObservableObject
{
    private bool _isRunning;
    private int _progress;
    private string _step = string.Empty;
    private BenchmarkResult? _before;
    private BenchmarkResult? _after;

    public BenchmarkViewModel()
    {
        History = new ObservableCollection<BenchmarkResult>(BenchmarkService.LoadHistory());
        _before = History.LastOrDefault(r => r.Label == "Before");
        _after = History.LastOrDefault(r => r.Label == "After");
        RunBeforeCommand = new AsyncRelayCommand(() => RunAsync("Before"), () => !IsRunning);
        RunAfterCommand = new AsyncRelayCommand(() => RunAsync("After"), () => !IsRunning);
        ClearCommand = new RelayCommand(() =>
        {
            History.Clear();
            _before = _after = null;
            BenchmarkService.SaveHistory(History);
            UpdateComparison();
        });
        UpdateComparison();
    }

    public ObservableCollection<BenchmarkResult> History { get; }
    public ObservableCollection<ComparisonRow> Comparison { get; } = new();

    public ICommand RunBeforeCommand { get; }
    public ICommand RunAfterCommand { get; }
    public ICommand ClearCommand { get; }

    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public int Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string Step { get => _step; private set => SetProperty(ref _step, value); }

    public string BeforeScore => _before?.Score.ToString() ?? "–";
    public string AfterScore => _after?.Score.ToString() ?? "–";
    public string ScoreDifference => _before != null && _after != null
        ? $"{(_after.Score - _before.Score >= 0 ? "+" : string.Empty)}{_after.Score - _before.Score} ({Pct(_before.Score, _after.Score)})"
        : "–";

    private async Task RunAsync(string label)
    {
        IsRunning = true;
        try
        {
            var progress = new Progress<(int percent, string step)>(p =>
            {
                Progress = p.percent;
                Step = p.step;
            });
            var result = await BenchmarkService.RunAsync(label, progress, CancellationToken.None);
            History.Add(result);
            BenchmarkService.SaveHistory(History);
            if (label == "Before")
            {
                _before = result;
                _after = null;
            }
            else
            {
                _after = result;
            }

            UpdateComparison();
            AppLog.Info($"Benchmark {label}: score {result.Score}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Benchmark failed", ex);
            Step = "The benchmark couldn't be completed.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void UpdateComparison()
    {
        Comparison.Clear();
        if (_before != null)
        {
            Add("Performance Score", _before.Score, _after?.Score, "0", true);
            Add("CPU single-thread (Mops/s)", _before.SingleThreadMops, _after?.SingleThreadMops, "0", true);
            Add("CPU multi-thread (Mops/s)", _before.MultiThreadMops, _after?.MultiThreadMops, "0", true);
            Add("Memory bandwidth (GB/s)", _before.MemoryGBps, _after?.MemoryGBps, "0.0", true);
            Add("Background CPU load (%)", _before.BackgroundCpuPercent, _after?.BackgroundCpuPercent, "0.0", false);
            Add("Scheduling jitter p90 (ms)", _before.SchedulingJitterMs, _after?.SchedulingJitterMs, "0.00", false);
        }

        OnPropertyChanged(nameof(BeforeScore));
        OnPropertyChanged(nameof(AfterScore));
        OnPropertyChanged(nameof(ScoreDifference));
    }

    private void Add(string metric, double before, double? after, string format, bool higherIsBetter)
    {
        string diff = "–";
        bool? better = null;
        if (after.HasValue)
        {
            double d = after.Value - before;
            diff = (d >= 0 ? "+" : string.Empty) + d.ToString(format) + $" ({Pct(before, after.Value)})";
            double relative = before == 0 ? 0 : Math.Abs(d / before);
            better = relative < 0.03 ? null : (d > 0) == higherIsBetter;
        }

        Comparison.Add(new ComparisonRow(metric, before.ToString(format), after?.ToString(format) ?? "–", diff, better));
    }

    private static string Pct(double before, double after) =>
        before == 0 ? "–" : $"{((after - before) / before * 100 >= 0 ? "+" : string.Empty)}{(after - before) / before * 100:0.0} %";
}
