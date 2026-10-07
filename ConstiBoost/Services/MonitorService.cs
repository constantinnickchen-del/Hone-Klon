using System;
using System.Threading;
using ConstiBoost.Hardware;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed record MonitorSnapshot(
    DateTime Timestamp,
    double? CpuPercent,
    double? GpuPercent,
    double? RamPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    ThermalReading? Thermal);

/// <summary>
/// Live sampling (~1 s). Runs only while at least one page that shows live data is visible,
/// so ConstiBoost uses practically no CPU when idle or minimized.
/// </summary>
public sealed class MonitorService
{
    private readonly object _gate = new();
    private readonly CpuUsageSampler _cpu = new();
    private readonly GpuUsageSampler _gpu = new();
    private readonly ThermalSampler _thermal = new();
    private Timer? _timer;
    private int _subscribers;
    private int _tick;
    private ThermalReading? _lastThermal;
    private int _busy;

    public static MonitorService Instance { get; } = new();

    public event Action<MonitorSnapshot>? Sampled;

    public MonitorSnapshot? Last { get; private set; }

    public bool GpuAvailable => _gpu.IsAvailable;

    public void Subscribe()
    {
        lock (_gate)
        {
            _subscribers++;
            if (_timer == null)
            {
                _cpu.Sample();
                _timer = new Timer(_ => Tick(), null, 0, 1000);
            }
        }
    }

    public void Unsubscribe()
    {
        lock (_gate)
        {
            _subscribers = Math.Max(0, _subscribers - 1);
            if (_subscribers == 0)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return; // previous sample still running
        }

        try
        {
            double? cpu = _cpu.Sample();
            _gpu.Sample();
            if (_tick++ % 3 == 0)
            {
                _lastThermal = _thermal.Sample();
            }

            var memory = SystemInfo.GetMemoryStatus();
            long total = memory.HasValue ? (long)memory.Value.total : 0;
            long used = memory.HasValue ? (long)(memory.Value.total - memory.Value.available) : 0;
            double? ramPct = total > 0 ? 100.0 * used / total : null;
            var snapshot = new MonitorSnapshot(DateTime.Now, cpu, _gpu.Total, ramPct, used, total, _lastThermal);
            Last = snapshot;
            Sampled?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Monitor tick failed: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }
}
