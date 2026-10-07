using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Hardware;

/// <summary>Reads hardware information through WMI and Win32. Values that cannot be read stay "Unavailable".</summary>
public static class HardwareScanner
{
    private static readonly Regex IntegratedAmd = new(@"Radeon\s*(\(TM\))?\s*(Vega\s*\d+\s*)?Graphics|Radeon\s+\d{3}M|Radeon\s+Vega\s+\d+(?!\s*(Frontier|56|64))", RegexOptions.IgnoreCase);
    private static readonly Regex IntegratedIntel = new(@"Intel.*(UHD|Iris|HD Graphics|\(R\) Graphics|Arc\(TM\) Graphics)", RegexOptions.IgnoreCase);
    private static readonly Regex VirtualAdapter = new(@"Basic Display|Basic Render|Virtual|Remote Display|Parsec|Meta|IddSample|Hyper-V|VMware|VirtualBox|Citrix|DisplayLink", RegexOptions.IgnoreCase);

    public static HardwareInfo Scan()
    {
        var info = new HardwareInfo();
        ReadCpu(info);
        ReadGpus(info);
        ReadMemory(info);
        ReadSystem(info);
        ReadOs(info);
        ReadDrives(info);
        var power = SystemInfo.GetPowerStatus();
        info.OnAcPower = power.onAc;
        info.BatteryPercent = power.batteryPercent;
        info.HasBattery = power.hasBattery;
        return info;
    }

    private static IEnumerable<ManagementBaseObject> Query(string wql, string scope = @"root\CIMV2")
    {
        var results = new List<ManagementBaseObject>();
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, wql);
            using var collection = searcher.Get();
            foreach (ManagementBaseObject item in collection)
            {
                results.Add(item);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"WMI query failed ({wql}): {ex.Message}");
        }

        return results;
    }

    private static string? Str(ManagementBaseObject o, string property)
    {
        try
        {
            return o[property]?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static long? Num(ManagementBaseObject o, string property)
    {
        try
        {
            object? value = o[property];
            return value is null ? null : Convert.ToInt64(value);
        }
        catch
        {
            return null;
        }
    }

    private static void ReadCpu(HardwareInfo info)
    {
        var cpu = Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor").FirstOrDefault();
        if (cpu is null)
        {
            return;
        }

        string? name = Str(cpu, "Name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            info.CpuName = Regex.Replace(name, @"\s+", " ");
        }

        info.CpuCores = (int?)Num(cpu, "NumberOfCores");
        info.CpuThreads = (int?)Num(cpu, "NumberOfLogicalProcessors");
        info.CpuMaxClockMhz = (int?)Num(cpu, "MaxClockSpeed");
    }

    private static void ReadGpus(HardwareInfo info)
    {
        foreach (var gpu in Query("SELECT Name, DriverVersion, CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution, AdapterCompatibility FROM Win32_VideoController"))
        {
            string name = Str(gpu, "Name") ?? Formatting.Unavailable;
            string compat = Str(gpu, "AdapterCompatibility") ?? string.Empty;
            var item = new GpuInfo
            {
                Name = name,
                DriverVersion = Str(gpu, "DriverVersion") ?? Formatting.Unavailable,
                RefreshRate = (int?)Num(gpu, "CurrentRefreshRate"),
                HorizontalResolution = (int?)Num(gpu, "CurrentHorizontalResolution"),
                VerticalResolution = (int?)Num(gpu, "CurrentVerticalResolution"),
                Vendor = DetectVendor(name + " " + compat),
            };
            item.IsIntegrated = IntegratedAmd.IsMatch(name) || IntegratedIntel.IsMatch(name);
            if (item.RefreshRate is <= 1)
            {
                item.RefreshRate = null;
            }

            if (VirtualAdapter.IsMatch(name) && !name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            info.Gpus.Add(item);
        }

        // Dedicated GPUs first so the "primary" GPU is the one games normally use.
        info.Gpus = info.Gpus
            .OrderBy(g => g.Name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase))
            .ThenBy(g => g.IsIntegrated)
            .ToList();
    }

    private static string DetectVendor(string text)
    {
        if (text.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return "NVIDIA";
        if (text.Contains("AMD", StringComparison.OrdinalIgnoreCase) || text.Contains("Radeon", StringComparison.OrdinalIgnoreCase) || text.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase)) return "AMD";
        if (text.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return "Intel";
        return "Unknown";
    }

    private static void ReadMemory(HardwareInfo info)
    {
        var status = SystemInfo.GetMemoryStatus();
        if (status.HasValue)
        {
            info.TotalRamBytes = (long)status.Value.total;
        }

        var modules = Query("SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory").ToList();
        if (modules.Count > 0)
        {
            info.RamModules = modules.Count;
            long? speed = modules.Select(m => Num(m, "ConfiguredClockSpeed") ?? Num(m, "Speed")).Where(s => s > 0).DefaultIfEmpty(null).Max();
            info.RamSpeedMhz = (int?)speed;
        }
    }

    private static void ReadSystem(HardwareInfo info)
    {
        var cs = Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem").FirstOrDefault();
        if (cs != null)
        {
            info.Manufacturer = Str(cs, "Manufacturer") ?? Formatting.Unavailable;
            info.Model = Str(cs, "Model") ?? Formatting.Unavailable;
        }
    }

    private static void ReadOs(HardwareInfo info)
    {
        var os = Query("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem").FirstOrDefault();
        if (os != null)
        {
            info.WindowsName = (Str(os, "Caption") ?? Formatting.Unavailable).Replace("Microsoft ", string.Empty);
            info.WindowsVersion = Str(os, "Version") ?? Formatting.Unavailable;
            if (int.TryParse(Str(os, "BuildNumber"), out int build))
            {
                info.WindowsBuild = build;
            }
        }

        info.WindowsBuild ??= SystemInfo.WindowsBuild;
        string? displayVersion = RegistryHelper.ReadString("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
        if (!string.IsNullOrWhiteSpace(displayVersion))
        {
            info.WindowsVersion = $"{displayVersion} (Build {info.WindowsBuild})";
        }
    }

    private static void ReadDrives(HardwareInfo info)
    {
        string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    {
                        continue;
                    }

                    info.Drives.Add(new DriveSummary
                    {
                        Name = drive.Name,
                        Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        Format = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.AvailableFreeSpace,
                        IsSystemDrive = string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase),
                    });
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Drive {drive.Name} could not be read: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Drives could not be enumerated: " + ex.Message);
        }
    }
}
