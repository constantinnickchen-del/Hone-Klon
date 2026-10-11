using System;
using System.Diagnostics;

namespace ConstiBoost.Hardware;

public sealed record GpuMemoryUsage(long DedicatedBytes, long SharedBytes);

/// <summary>GPU memory in use from the Windows "GPU Adapter Memory" performance counters.</summary>
public static class GpuMemoryReader
{
    public static GpuMemoryUsage? Read()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Adapter Memory"))
            {
                return null;
            }

            var data = new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory();
            long dedicated = Sum(data["dedicated usage"]);
            long shared = Sum(data["shared usage"]);
            return new GpuMemoryUsage(dedicated, shared);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long Sum(InstanceDataCollection? collection)
    {
        long total = 0;
        if (collection is null)
        {
            return 0;
        }

        foreach (InstanceData instance in collection.Values)
        {
            total += instance.RawValue;
        }

        return total;
    }
}
