using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConstiBoost.Hardware;

/// <summary>
/// GPU utilization from the Windows "GPU Engine" performance counters (the same source Task Manager uses).
/// Utilization = busiest engine, per-process = busiest engine of that process.
/// </summary>
public sealed class GpuUsageSampler
{
    private static readonly Regex PidRegex = new(@"^pid_(\d+)_", RegexOptions.Compiled);
    private Dictionary<string, CounterSample> _previous = new();
    private static readonly Lazy<bool> CategoryExists = new(() =>
    {
        try
        {
            return PerformanceCounterCategory.Exists("GPU Engine");
        }
        catch
        {
            return false;
        }
    });

    private bool _available = true;

    public bool IsAvailable => _available;

    public double? Total { get; private set; }

    public IReadOnlyDictionary<int, double> PerProcess { get; private set; } = new Dictionary<int, double>();

    public void Sample()
    {
        if (!_available)
        {
            return;
        }

        try
        {
            if (!CategoryExists.Value)
            {
                _available = false;
                Total = null;
                return;
            }

            var category = new PerformanceCounterCategory("GPU Engine");
            var data = category.ReadCategory();
            var util = data["utilization percentage"];
            if (util is null)
            {
                Total = null;
                return;
            }

            var current = new Dictionary<string, CounterSample>(util.Count);
            var engines = new Dictionary<string, double>();
            var perProcess = new Dictionary<int, double>();
            foreach (InstanceData instance in util.Values)
            {
                string name = instance.InstanceName;
                current[name] = instance.Sample;
                if (!_previous.TryGetValue(name, out var before))
                {
                    continue;
                }

                double value = CounterSample.Calculate(before, instance.Sample);
                if (double.IsNaN(value) || value < 0)
                {
                    continue;
                }

                var match = PidRegex.Match(name);
                string engineKey = match.Success ? name[match.Length..] : name;
                engines[engineKey] = engines.TryGetValue(engineKey, out double sum) ? sum + value : value;
                if (match.Success && int.TryParse(match.Groups[1].Value, out int pid))
                {
                    perProcess[pid] = Math.Max(perProcess.TryGetValue(pid, out double p) ? p : 0, value);
                }
            }

            bool hadBaseline = _previous.Count > 0;
            _previous = current;
            Total = hadBaseline ? Math.Min(100, engines.Values.DefaultIfEmpty(0).Max()) : null;
            PerProcess = perProcess;
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("GPU counters unavailable: " + ex.Message);
            _available = false;
            Total = null;
        }
    }
}
