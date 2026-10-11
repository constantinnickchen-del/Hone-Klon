using System;
using System.Collections.Generic;
using System.Management;

namespace ConstiBoost.Hardware;

public sealed record DriveMedia(string Letter, string MediaType, string BusType);

/// <summary>Maps drive letters to the physical disk type (HDD / SSD / NVMe) via the Windows Storage WMI provider.</summary>
public static class StorageInfo
{
    public static List<DriveMedia> Read()
    {
        var result = new List<DriveMedia>();
        try
        {
            var disks = new Dictionary<string, (string media, string bus)>();
            using (var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, MediaType, BusType FROM MSFT_PhysicalDisk"))
            using (var items = searcher.Get())
            {
                foreach (ManagementBaseObject d in items)
                {
                    using (d)
                    {
                        string id = d["DeviceId"]?.ToString() ?? string.Empty;
                        int media = Convert.ToInt32(d["MediaType"] ?? 0);
                        int bus = Convert.ToInt32(d["BusType"] ?? 0);
                        disks[id] = (media switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Unknown" }, bus switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", 8 => "RAID", _ => "Other" });
                    }
                }
            }

            using var partitions = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DriveLetter, DiskNumber FROM MSFT_Partition");
            using var parts = partitions.Get();
            foreach (ManagementBaseObject p in parts)
            {
                using (p)
                {
                    char letter = p["DriveLetter"] switch { char c => c, ushort u => (char)u, string s when s.Length > 0 => s[0], _ => '\0' };
                    if (letter == '\0' || !char.IsLetter(letter))
                    {
                        continue;
                    }

                    string disk = p["DiskNumber"]?.ToString() ?? string.Empty;
                    if (disks.TryGetValue(disk, out var info))
                    {
                        string media = info.bus == "NVMe" && info.media == "Unknown" ? "SSD" : info.media;
                        result.Add(new DriveMedia($"{letter}:", media, info.bus));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Storage info unavailable: " + ex.Message);
        }

        return result;
    }
}
