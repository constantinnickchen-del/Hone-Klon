using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace ConstiBoost.Hardware;

public sealed record MemoryModule(long CapacityBytes, int? SpeedMts, string Type, string FormFactor, string Manufacturer, string PartNumber, string Slot);

public sealed record MemoryLayout(int? Slots, List<MemoryModule> Modules)
{
    public int? FreeSlots => Slots is int s ? Math.Max(0, s - Modules.Count) : null;
}

/// <summary>RAM slots and installed modules from WMI (Win32_PhysicalMemoryArray / Win32_PhysicalMemory).</summary>
public static class MemoryInfo
{
    public static MemoryLayout Read()
    {
        int? slots = null;
        var modules = new List<MemoryModule>();
        try
        {
            using (var arrays = new ManagementObjectSearcher("SELECT MemoryDevices, Use FROM Win32_PhysicalMemoryArray"))
            using (var results = arrays.Get())
            {
                int total = 0;
                foreach (ManagementBaseObject a in results)
                {
                    using (a)
                    {
                        // Use = 3 → system memory
                        if (a["Use"] is ushort use && use != 3)
                        {
                            continue;
                        }

                        total += Convert.ToInt32(a["MemoryDevices"] ?? 0);
                    }
                }

                slots = total > 0 ? total : null;
            }

            using var searcher = new ManagementObjectSearcher("SELECT Capacity, ConfiguredClockSpeed, Speed, SMBIOSMemoryType, FormFactor, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory");
            using var mods = searcher.Get();
            foreach (ManagementBaseObject m in mods)
            {
                using (m)
                {
                    long capacity = Convert.ToInt64(m["Capacity"] ?? 0L);
                    int? speed = m["ConfiguredClockSpeed"] is uint cs && cs > 0 ? (int)cs : m["Speed"] is uint sp && sp > 0 ? (int)sp : null;
                    int type = Convert.ToInt32(m["SMBIOSMemoryType"] ?? 0);
                    int form = Convert.ToInt32(m["FormFactor"] ?? 0);
                    modules.Add(new MemoryModule(capacity, speed,
                        type switch { 26 => "DDR4", 34 => "DDR5", 24 => "DDR3", 30 => "LPDDR4", 35 => "LPDDR5", _ => "Unknown" },
                        form switch { 12 => "SO-DIMM", 8 => "DIMM", 0 => "Soldered/unknown", _ => "Other" },
                        (m["Manufacturer"]?.ToString() ?? string.Empty).Trim(),
                        (m["PartNumber"]?.ToString() ?? string.Empty).Trim(),
                        (m["DeviceLocator"]?.ToString() ?? string.Empty).Trim()));
                }
            }
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Memory layout unavailable: " + ex.Message);
        }

        return new MemoryLayout(slots, modules);
    }
}
