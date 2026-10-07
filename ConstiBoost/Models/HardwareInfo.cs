using System.Collections.Generic;

namespace ConstiBoost.Models;

public sealed class HardwareInfo
{
    public string CpuName { get; set; } = Core.Formatting.Unavailable;
    public int? CpuCores { get; set; }
    public int? CpuThreads { get; set; }
    public int? CpuMaxClockMhz { get; set; }

    public List<GpuInfo> Gpus { get; set; } = new();

    public long? TotalRamBytes { get; set; }
    public int? RamSpeedMhz { get; set; }
    public int? RamModules { get; set; }

    public string Manufacturer { get; set; } = Core.Formatting.Unavailable;
    public string Model { get; set; } = Core.Formatting.Unavailable;

    public string WindowsName { get; set; } = Core.Formatting.Unavailable;
    public string WindowsVersion { get; set; } = Core.Formatting.Unavailable;
    public int? WindowsBuild { get; set; }

    public List<DriveSummary> Drives { get; set; } = new();

    public bool? OnAcPower { get; set; }
    public int? BatteryPercent { get; set; }
    public bool HasBattery { get; set; }

    public string PrimaryGpuName => Gpus.Count > 0 ? Gpus[0].Name : Core.Formatting.Unavailable;

    public bool IsWindows11 => WindowsBuild >= 22000;
}

public sealed class GpuInfo
{
    public string Name { get; set; } = Core.Formatting.Unavailable;
    public string DriverVersion { get; set; } = Core.Formatting.Unavailable;
    public int? RefreshRate { get; set; }
    public int? HorizontalResolution { get; set; }
    public int? VerticalResolution { get; set; }
    public bool IsIntegrated { get; set; }
    public string Vendor { get; set; } = "Unknown";
}

public sealed class DriveSummary
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public bool IsSystemDrive { get; set; }

    public double UsedPercent => TotalBytes > 0 ? 100.0 * (TotalBytes - FreeBytes) / TotalBytes : 0;

    public string Summary => $"{Core.Formatting.Bytes(FreeBytes)} free of {Core.Formatting.Bytes(TotalBytes)}";
}
