namespace ConstiBoost.Models;

/// <summary>Snapshot of gaming-relevant Windows settings at scan time.</summary>
public sealed class SystemState
{
    public bool? GameModeEnabled { get; set; }
    public bool? GameBarEnabled { get; set; }
    public bool? GameDvrEnabled { get; set; }
    public bool? BackgroundRecordingEnabled { get; set; }
    public bool? HagsEnabled { get; set; }
    public string HagsText { get; set; } = Core.Formatting.Unavailable;
    public string PowerPlanName { get; set; } = Core.Formatting.Unavailable;
    public string PowerModeName { get; set; } = Core.Formatting.Unavailable;
    public bool? TransparencyEnabled { get; set; }
    public int EnabledStartupApps { get; set; }
    public int TotalStartupApps { get; set; }
    public double? IdleCpuPercent { get; set; }
    public double? RamUsedPercent { get; set; }
    public long? RamAvailableBytes { get; set; }
    public double? SystemDriveFreePercent { get; set; }
}
