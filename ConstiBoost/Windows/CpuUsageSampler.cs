namespace ConstiBoost.WinSys;

/// <summary>Total CPU usage from GetSystemTimes – very cheap, no performance counters needed.</summary>
public sealed class CpuUsageSampler
{
    private ulong _lastIdle;
    private ulong _lastTotal;
    private bool _hasBaseline;

    public double? Sample()
    {
        try
        {
            if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return null;
            }

            ulong idleValue = idle.Value;
            ulong total = kernel.Value + user.Value; // kernel time includes idle time
            if (!_hasBaseline)
            {
                _lastIdle = idleValue;
                _lastTotal = total;
                _hasBaseline = true;
                return null;
            }

            ulong idleDelta = idleValue - _lastIdle;
            ulong totalDelta = total - _lastTotal;
            _lastIdle = idleValue;
            _lastTotal = total;
            if (totalDelta == 0)
            {
                return null;
            }

            double usage = 100.0 * (totalDelta - idleDelta) / totalDelta;
            return usage < 0 ? 0 : usage > 100 ? 100 : usage;
        }
        catch
        {
            return null;
        }
    }
}
