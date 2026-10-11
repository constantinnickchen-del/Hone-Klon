using System.Collections.Generic;

namespace ConstiBoost.Hardware.Sensors;

public sealed record SensorValue(string Name, double Value, string Unit, string Source);

/// <summary>
/// One reading of all available hardware sensors. Every value is either a real reading or null.
/// Nothing in here is estimated.
/// </summary>
public sealed class SensorSnapshot
{
    /// <summary>CPU temperature in °C from a direct CPU sensor (HWiNFO / LibreHardwareMonitor), or null.</summary>
    public double? CpuTemperature { get; init; }

    public string? CpuTemperatureLabel { get; init; }

    public double? GpuTemperature { get; init; }

    public double? GpuClockMhz { get; init; }

    public IReadOnlyList<SensorValue> Fans { get; init; } = new List<SensorValue>();

    /// <summary>ACPI thermal zone (firmware sensor; often close to CPU on laptops, but not guaranteed).</summary>
    public ThermalReading? AcpiZone { get; init; }

    /// <summary>Name of the provider that delivered the direct sensor values, e.g. "HWiNFO".</summary>
    public string? Provider { get; init; }

    public bool HasDirectCpuSensor => CpuTemperature.HasValue;

    /// <summary>Best available temperature for thermal analysis: direct CPU sensor first, then the ACPI zone.</summary>
    public double? BestTemperature => CpuTemperature ?? AcpiZone?.Celsius;

    public string BestTemperatureSource => CpuTemperature.HasValue
        ? $"{Provider}: {CpuTemperatureLabel}"
        : AcpiZone != null
            ? $"Windows ACPI thermal zone {AcpiZone.Zone} (indirect – may differ from the CPU die)"
            : "Sensor unavailable";

    public static SensorSnapshot Empty { get; } = new();
}

public interface ISensorProvider
{
    string Name { get; }

    /// <summary>Returns null when the provider is not running / not available.</summary>
    SensorSnapshot? Read();
}
