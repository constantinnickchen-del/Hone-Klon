using System;
using System.Collections.Generic;
using System.Management;

namespace ConstiBoost.Hardware.Sensors;

/// <summary>
/// Reads sensors that LibreHardwareMonitor / OpenHardwareMonitor publish through WMI while they are running.
/// Read-only – ConstiBoost does not ship or load their drivers.
/// </summary>
public sealed class HardwareMonitorWmiProvider : ISensorProvider
{
    private readonly string _namespace;

    public HardwareMonitorWmiProvider(string name, string wmiNamespace)
    {
        Name = name;
        _namespace = wmiNamespace;
    }

    public string Name { get; }

    public SensorSnapshot? Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(_namespace, "SELECT Name, SensorType, Value, Parent FROM Sensor");
            using var results = searcher.Get();
            double? cpuTemp = null;
            string? cpuLabel = null;
            int cpuRank = int.MaxValue;
            double? gpuTemp = null;
            double? gpuClock = null;
            var fans = new List<SensorValue>();

            foreach (ManagementBaseObject sensor in results)
            {
                using (sensor)
                {
                    string type = sensor["SensorType"]?.ToString() ?? string.Empty;
                    string name = sensor["Name"]?.ToString() ?? string.Empty;
                    string parent = sensor["Parent"]?.ToString()?.ToLowerInvariant() ?? string.Empty;
                    if (sensor["Value"] is not float raw || float.IsNaN(raw))
                    {
                        continue;
                    }

                    double value = raw;
                    bool isCpu = parent.Contains("cpu");
                    bool isGpu = parent.Contains("gpu");
                    switch (type)
                    {
                        case "Temperature" when isCpu && value is > 1 and < 125:
                            int rank = name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ? 0
                                : name.Contains("Package", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
                            if (rank < cpuRank)
                            {
                                cpuRank = rank;
                                cpuTemp = value;
                                cpuLabel = name;
                            }

                            break;
                        case "Temperature" when isGpu && value is > 1 and < 125:
                            gpuTemp ??= value;
                            break;
                        case "Fan" when value is >= 0 and < 20000:
                            fans.Add(new SensorValue(name, value, "RPM", Name));
                            break;
                        case "Clock" when isGpu && name.Contains("Core", StringComparison.OrdinalIgnoreCase) && value > 0:
                            gpuClock ??= value;
                            break;
                    }
                }
            }

            if (cpuTemp is null && gpuTemp is null && fans.Count == 0 && gpuClock is null)
            {
                return null;
            }

            return new SensorSnapshot
            {
                CpuTemperature = cpuTemp,
                CpuTemperatureLabel = cpuLabel,
                GpuTemperature = gpuTemp,
                GpuClockMhz = gpuClock,
                Fans = fans,
                Provider = Name,
            };
        }
        catch (ManagementException)
        {
            return null; // namespace missing = tool not running
        }
        catch (Exception)
        {
            return null;
        }
    }
}
