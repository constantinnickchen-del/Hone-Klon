using System;
using System.Collections.Generic;
using ConstiBoost.Core;
using ConstiBoost.Localization;

namespace ConstiBoost.Models;

public enum TemperatureLevel
{
    Unavailable,
    Cool,
    Normal,
    Warm,
    Hot,
    Critical,
}

public enum ThermalAlertLevel
{
    None,
    High,
    Critical,
}

public static class TemperatureLevels
{
    /// <summary>Guide values only – not a technical guarantee.</summary>
    public static TemperatureLevel Classify(double? celsius) => celsius switch
    {
        null => TemperatureLevel.Unavailable,
        < 60 => TemperatureLevel.Cool,
        < 75 => TemperatureLevel.Normal,
        < 85 => TemperatureLevel.Warm,
        < 95 => TemperatureLevel.Hot,
        _ => TemperatureLevel.Critical,
    };

    public static string Text(TemperatureLevel level) => level switch
    {
        TemperatureLevel.Cool => L.T("Cool"),
        TemperatureLevel.Normal => L.T("Normal"),
        TemperatureLevel.Warm => L.T("Warm"),
        TemperatureLevel.Hot => L.T("Hot"),
        TemperatureLevel.Critical => L.T("Critical"),
        _ => L.T("Sensor unavailable"),
    };

    public static CheckStatus Status(TemperatureLevel level) => level switch
    {
        TemperatureLevel.Cool or TemperatureLevel.Normal => CheckStatus.Good,
        TemperatureLevel.Warm => CheckStatus.Warning,
        TemperatureLevel.Hot or TemperatureLevel.Critical => CheckStatus.Bad,
        _ => CheckStatus.Unavailable,
    };
}

/// <summary>One live measurement. All values come from Windows or a running sensor tool; null = unavailable.</summary>
public sealed record ThermalSample(
    DateTime Time,
    double? Temperature,
    bool DirectSensor,
    double? CpuLoad,
    double? GpuLoad,
    double? RamPercent,
    double? CpuMhz,
    bool GameRunning);

public sealed record MinuteSummary(string Time, string Temperature, string CpuLoad, string Phase);

public sealed record ProcessUsage(string Name, double Cpu, long Ram, double? Gpu, int Count, bool IsBackground, bool IsGame, bool IsSystem)
{
    public string CpuText => $"{Cpu:0.0} %";
    public string RamText => Formatting.Bytes(Ram);
    public string GpuText => Gpu.HasValue ? $"{Gpu:0} %" : "–";
    public string Kind => IsGame ? L.T("Game") : IsSystem ? L.T("Windows/system") : IsBackground ? L.T("Background") : L.T("App");
}

public sealed class ThermalCause
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CheckStatus Severity { get; init; } = CheckStatus.Warning;
    public List<string> Details { get; init; } = new();
}

/// <summary>Values measured before/after an optimization or during a thermal test.</summary>
public sealed class ThermalMeasurement
{
    public DateTime Time { get; init; } = DateTime.Now;
    public double? StartTemperature { get; init; }
    public double? AverageTemperature { get; init; }
    public double? MaxTemperature { get; init; }
    public double? FinalTemperature { get; init; }
    public double? AverageCpuLoad { get; init; }
    public double? AverageCpuMhz { get; init; }
    public double? AverageGpuLoad { get; init; }
    public double? RamPercent { get; init; }
    public int? BackgroundProcesses { get; init; }
    public string Source { get; init; } = string.Empty;
    public bool HasTemperature => AverageTemperature.HasValue;
}

public sealed class ThermalTestRecord
{
    public DateTime Time { get; set; }
    public string Profile { get; set; } = "Normal";
    public double? StartTemperature { get; set; }
    public double? AverageTemperature { get; set; }
    public double? MaxTemperature { get; set; }
    public double? FinalTemperature { get; set; }
    public double? AverageCpuLoad { get; set; }
    public double? AverageCpuMhz { get; set; }
    public bool GameRunning { get; set; }
    public string Source { get; set; } = string.Empty;
}

public enum GamingProfile
{
    MaxFps,
    Balanced,
    Cool,
}
