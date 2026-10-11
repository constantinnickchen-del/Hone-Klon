using System;
using System.Diagnostics;

namespace ConstiBoost.Hardware;

/// <summary>
/// Effective CPU clock as Task Manager shows it: "% Processor Performance" (Processor Information, _Total)
/// multiplied by the nominal clock reported by Windows.
/// </summary>
public sealed class CpuFrequencySampler : IDisposable
{
    private readonly int? _baseMhz;
    private PerformanceCounter? _counter;
    private bool _failed;

    public CpuFrequencySampler(int? baseMhz) => _baseMhz = baseMhz;

    public double? Sample()
    {
        if (_failed || _baseMhz is not > 0)
        {
            return null;
        }

        try
        {
            if (_counter is null)
            {
                _counter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
                _counter.NextValue(); // first value is always 0
                return null;
            }

            double pct = _counter.NextValue();
            return pct > 0 ? pct / 100.0 * _baseMhz.Value : null;
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("CPU frequency counter unavailable: " + ex.Message);
            _failed = true;
            return null;
        }
    }

    public void Dispose() => _counter?.Dispose();
}
