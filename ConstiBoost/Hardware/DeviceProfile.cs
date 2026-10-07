using System;
using System.Linq;
using System.Text.RegularExpressions;
using ConstiBoost.Models;

namespace ConstiBoost.Hardware;

/// <summary>
/// Classifies the PC so recommendations fit the hardware. Nothing here "unlocks" hardware –
/// it only decides which safe settings make sense.
/// </summary>
public sealed class DeviceProfile
{
    private static readonly Regex Ryzen5000Mobile = new(@"Ryzen\s+[3579]\s+5\d{3}[UH]", RegexOptions.IgnoreCase);
    private static readonly Regex Ryzen5Series5000 = new(@"Ryzen\s+5\s+5\d{3}", RegexOptions.IgnoreCase);

    public bool HasDedicatedGpu { get; private init; }
    public bool IsLowPowerIntegrated { get; private init; }
    public bool IsAcerAspire3Match { get; private init; }
    public bool IsLaptop { get; private init; }
    public string Vendor { get; private init; } = "Unknown";
    public string Title { get; private init; } = string.Empty;
    public string Description { get; private init; } = string.Empty;

    public static DeviceProfile From(HardwareInfo hw)
    {
        bool dedicated = hw.Gpus.Any(g => !g.IsIntegrated && !g.Name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) && g.Vendor != "Unknown");
        bool integratedOnly = !dedicated && hw.Gpus.Any(g => g.IsIntegrated);
        bool lowRam = hw.TotalRamBytes is > 0 and <= 8L * 1024 * 1024 * 1024 + 512L * 1024 * 1024;
        bool mobileCpu = Regex.IsMatch(hw.CpuName, @"\d{4,5}(U|H|HS|HX|P)\b", RegexOptions.IgnoreCase);
        bool acer = hw.Manufacturer.Contains("Acer", StringComparison.OrdinalIgnoreCase);
        bool aspire3 = acer && Regex.IsMatch(hw.Model, @"Aspire\s*(A)?3|A315", RegexOptions.IgnoreCase);
        bool ryzenMatch = Ryzen5000Mobile.IsMatch(hw.CpuName) || Ryzen5Series5000.IsMatch(hw.CpuName);
        bool amdIgpu = hw.Gpus.Any(g => g.IsIntegrated && g.Vendor == "AMD");

        bool lowPower = integratedOnly || (lowRam && !dedicated);
        string title;
        string description;
        if (aspire3 && ryzenMatch && amdIgpu && !dedicated)
        {
            title = "Acer Aspire 3 · Ryzen 5000 · Radeon Graphics";
            description = "We detected a low-power integrated-GPU system. The LOW-END GAMING PROFILE is recommended: stable FPS, less background load, lower temperatures, no unnecessary Windows effects.";
        }
        else if (lowPower)
        {
            title = "Integrated graphics system";
            description = "We detected a low-power integrated-GPU system. The LOW-END GAMING PROFILE is recommended: stable FPS, less background load, lower temperatures, no unnecessary Windows effects.";
        }
        else if (dedicated)
        {
            title = "Dedicated graphics system";
            description = "A dedicated GPU was detected. Balanced recommendations are used; visual effects stay untouched unless you choose otherwise.";
        }
        else
        {
            title = "Standard system";
            description = "Hardware could not be fully classified. Only general, safe recommendations are shown.";
        }

        return new DeviceProfile
        {
            HasDedicatedGpu = dedicated,
            IsLowPowerIntegrated = lowPower,
            IsAcerAspire3Match = aspire3 && ryzenMatch,
            IsLaptop = hw.HasBattery || mobileCpu,
            Vendor = hw.Gpus.FirstOrDefault()?.Vendor ?? "Unknown",
            Title = title,
            Description = description,
        };
    }
}
