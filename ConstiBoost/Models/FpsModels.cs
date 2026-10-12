using System;
using System.Collections.Generic;
using ConstiBoost.Localization;

namespace ConstiBoost.Models;

/// <summary>Result of a real frame capture (ETW present events).</summary>
public sealed class FpsResult
{
    public string ProcessName { get; init; } = string.Empty;
    public double Seconds { get; init; }
    public int Frames { get; init; }
    public double AverageFps { get; init; }
    public double Low1 { get; init; }
    public double Low01 { get; init; }
    public double MinFps { get; init; }
    public double MaxFps { get; init; }
    public int Stutters { get; init; }
    public double MedianFrameTimeMs { get; init; }
    public List<double> PerSecond { get; init; } = new();

    public string Summary => L.F("{0:0} FPS avg · 1 % low {1:0} · 0.1 % low {2:0} · {3} stutter(s)", AverageFps, Low1, Low01, Stutters);
}

/// <summary>Stored measurement for history, before/after and estimate calibration.</summary>
public sealed class FpsRecord
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public string Profile { get; set; } = "Normal";
    public string Setting { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public int RenderScale { get; set; } = 100;
    public double AverageFps { get; set; }
    public double Low1 { get; set; }
    public double Low01 { get; set; }
    public int Stutters { get; set; }
    public double Seconds { get; set; }
    public double? AverageTemperature { get; set; }
    public double? MaxTemperature { get; set; }
    public double? AverageCpuMhz { get; set; }
    public string Note { get; set; } = string.Empty;

    public string TimeText => Time.ToString("dd.MM. HH:mm");
    public string FpsText => $"{AverageFps:0} FPS";
    public string LowText => $"{Low1:0}";
    public string TempText => AverageTemperature.HasValue ? $"{AverageTemperature:0} °C" : "–";
}
