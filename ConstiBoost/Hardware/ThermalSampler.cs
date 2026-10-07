using System;
using System.Diagnostics;

namespace ConstiBoost.Hardware;

public sealed record ThermalReading(double Celsius, string Zone);

/// <summary>
/// Reads the ACPI "Thermal Zone Information" counters. These are real firmware sensors but are
/// usually NOT the CPU die temperature, so the UI labels them as "ACPI thermal zone".
/// Precise CPU/GPU die temperatures require vendor kernel drivers, which ConstiBoost does not install.
/// </summary>
public sealed class ThermalSampler
{
    private static readonly Lazy<bool> CategoryExists = new(() =>
    {
        try
        {
            return PerformanceCounterCategory.Exists("Thermal Zone Information");
        }
        catch
        {
            return false;
        }
    });

    private bool _available = true;

    public ThermalReading? Sample()
    {
        if (!_available)
        {
            return null;
        }

        try
        {
            if (!CategoryExists.Value)
            {
                _available = false;
                return null;
            }

            var category = new PerformanceCounterCategory("Thermal Zone Information");
            var data = category.ReadCategory();
            var precise = data["high precision temperature"];
            var basic = data["temperature"];
            ThermalReading? best = null;
            var source = precise ?? basic;
            if (source is null)
            {
                return null;
            }

            foreach (InstanceData instance in source.Values)
            {
                double kelvin = source == precise ? instance.RawValue / 10.0 : instance.RawValue;
                double celsius = kelvin - 273.15;
                if (celsius is < 5 or > 115)
                {
                    continue; // implausible – firmware placeholder values are ignored
                }

                if (best is null || celsius > best.Celsius)
                {
                    best = new ThermalReading(celsius, instance.InstanceName.Replace(@"\_TZ.", string.Empty));
                }
            }

            return best;
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Thermal counters unavailable: " + ex.Message);
            _available = false;
            return null;
        }
    }
}
