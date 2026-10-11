using System;
using System.Collections.Generic;

namespace ConstiBoost.Hardware.Sensors;

/// <summary>
/// Picks the best real sensor source available on this PC:
/// 1. HWiNFO shared memory, 2. LibreHardwareMonitor WMI, 3. OpenHardwareMonitor WMI,
/// plus the Windows ACPI thermal zone as an indirect fallback. Never estimates values.
/// </summary>
public sealed class SensorHub
{
    private static readonly TimeSpan RediscoverInterval = TimeSpan.FromSeconds(20);
    private readonly List<ISensorProvider> _providers = new()
    {
        new HwInfoSensorProvider(),
        new HardwareMonitorWmiProvider("LibreHardwareMonitor", @"root\LibreHardwareMonitor"),
        new HardwareMonitorWmiProvider("OpenHardwareMonitor", @"root\OpenHardwareMonitor"),
    };

    private readonly ThermalSampler _acpi = new();
    private readonly object _gate = new();
    private ISensorProvider? _active;
    private DateTime _lastDiscovery = DateTime.MinValue;

    public static SensorHub Instance { get; } = new();

    public string? ActiveProvider => _active?.Name;

    public SensorSnapshot Read()
    {
        lock (_gate)
        {
            SensorSnapshot? direct = null;
            if (_active != null)
            {
                direct = _active.Read();
                if (direct is null)
                {
                    _active = null; // tool was closed
                }
            }

            if (_active is null && DateTime.UtcNow - _lastDiscovery > RediscoverInterval)
            {
                _lastDiscovery = DateTime.UtcNow;
                foreach (var provider in _providers)
                {
                    direct = provider.Read();
                    if (direct != null)
                    {
                        _active = provider;
                        break;
                    }
                }
            }

            var acpi = _acpi.Sample();
            if (direct is null)
            {
                return new SensorSnapshot { AcpiZone = acpi };
            }

            return new SensorSnapshot
            {
                CpuTemperature = direct.CpuTemperature,
                CpuTemperatureLabel = direct.CpuTemperatureLabel,
                GpuTemperature = direct.GpuTemperature,
                GpuClockMhz = direct.GpuClockMhz,
                Fans = direct.Fans,
                Provider = direct.Provider,
                AcpiZone = acpi,
            };
        }
    }
}
