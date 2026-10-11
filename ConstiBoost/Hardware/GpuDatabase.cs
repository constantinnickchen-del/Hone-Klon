using System.Linq;
using System.Text.RegularExpressions;
using ConstiBoost.Models;

namespace ConstiBoost.Hardware;

public sealed record GpuClass(string Name, double Index, bool Integrated);

/// <summary>
/// Approximate relative gaming performance of common GPUs (1.0 = Radeon Vega 8 iGPU of a Ryzen 7 5700U).
/// Used only for clearly labelled FPS estimates – never shown as a measurement.
/// </summary>
public static class GpuDatabase
{
    private static readonly (Regex pattern, double index, bool igpu)[] Table =
    {
        // AMD integrated (named)
        (R(@"Radeon\s*890M"), 4.0, true), (R(@"Radeon\s*880M"), 3.6, true), (R(@"Radeon\s*780M"), 3.0, true), (R(@"Radeon\s*760M"), 2.5, true),
        (R(@"Radeon\s*740M"), 1.5, true), (R(@"Radeon\s*680M"), 2.2, true), (R(@"Radeon\s*660M"), 1.5, true), (R(@"Radeon\s*610M"), 0.45, true),
        (R(@"Vega\s*11"), 1.0, true), (R(@"Vega\s*10"), 0.9, true), (R(@"Vega\s*8"), 0.85, true), (R(@"Vega\s*6"), 0.65, true), (R(@"Vega\s*3"), 0.35, true),
        // Intel integrated
        (R(@"Arc\(TM\)\s*Graphics|Intel.*Arc.*130V|Intel.*Arc.*140V"), 2.2, true), (R(@"Iris\(R\)\s*Xe|Iris Xe"), 1.0, true), (R(@"Iris\(R\)\s*Plus|Iris Plus"), 0.6, true),
        (R(@"UHD Graphics\s*6\d\d"), 0.35, true), (R(@"UHD Graphics"), 0.45, true), (R(@"HD Graphics"), 0.3, true),
        // NVIDIA
        (R(@"RTX\s*4090"), 22, false), (R(@"RTX\s*4080"), 17, false), (R(@"RTX\s*4070"), 12.5, false), (R(@"RTX\s*4060"), 9.5, false), (R(@"RTX\s*4050"), 7, false),
        (R(@"RTX\s*50[89]0"), 20, false), (R(@"RTX\s*5070"), 14, false), (R(@"RTX\s*5060"), 11, false), (R(@"RTX\s*5050"), 8, false),
        (R(@"RTX\s*3090"), 15, false), (R(@"RTX\s*3080"), 13, false), (R(@"RTX\s*3070"), 10.5, false), (R(@"RTX\s*3060"), 7.8, false),
        (R(@"RTX\s*3050\s*Ti"), 4.6, false), (R(@"RTX\s*3050"), 4.6, false), (R(@"RTX\s*2080"), 9, false), (R(@"RTX\s*2070"), 7.5, false),
        (R(@"RTX\s*2060"), 6, false), (R(@"RTX\s*2050"), 2.8, false), (R(@"GTX\s*1660"), 4.5, false), (R(@"GTX\s*1650\s*Ti"), 3.4, false),
        (R(@"GTX\s*1650"), 3.1, false), (R(@"GTX\s*1080"), 6, false), (R(@"GTX\s*1070"), 4.9, false), (R(@"GTX\s*1060"), 3.7, false),
        (R(@"GTX\s*1050\s*Ti"), 2.4, false), (R(@"GTX\s*1050"), 2.0, false), (R(@"GTX\s*9[78]0"), 3.1, false), (R(@"GTX\s*960"), 2.2, false),
        (R(@"MX\s*5[57]0"), 1.7, false), (R(@"MX\s*450"), 1.6, false), (R(@"MX\s*350"), 1.1, false), (R(@"MX\s*[12]50|MX\s*1[13]0"), 0.9, false), (R(@"GT\s*1030"), 1.0, false),
        // AMD discrete
        (R(@"RX\s*79\d\d"), 18, false), (R(@"RX\s*78\d\d"), 13, false), (R(@"RX\s*77\d\d"), 11, false), (R(@"RX\s*76\d\d"), 8.5, false),
        (R(@"RX\s*69\d\d|RX\s*68\d\d"), 12, false), (R(@"RX\s*67\d\d"), 9, false), (R(@"RX\s*66\d\d"), 7, false), (R(@"RX\s*65\d\d"), 3.5, false),
        (R(@"RX\s*64\d\d"), 2.9, false), (R(@"RX\s*57\d\d"), 6.5, false), (R(@"RX\s*56\d\d"), 5.5, false), (R(@"RX\s*55\d\d"), 4.0, false),
        (R(@"RX\s*5[78]0"), 3.5, false), (R(@"RX\s*560"), 1.6, false), (R(@"RX\s*550"), 1.1, false),
    };

    /// <summary>Ryzen APUs report only "AMD Radeon(TM) Graphics" – the CPU model tells which iGPU it is.</summary>
    private static readonly (Regex cpu, double index, string name)[] AmdApu =
    {
        (R(@"Ryzen\s+\d\s+(8\d40|7\d40)"), 3.0, "Radeon 780M/760M class"),
        (R(@"Ryzen\s+\d\s+(6\d00|7\d35)"), 2.0, "Radeon 680M/660M class"),
        (R(@"Ryzen\s+7\s+5[78][02]5?U|Ryzen\s+7\s+7730U"), 1.0, "Radeon Vega 8 (8 CU)"),
        (R(@"Ryzen\s+5\s+5[56][02]5?U|Ryzen\s+5\s+7530U"), 0.85, "Radeon Vega 7 (7 CU)"),
        (R(@"Ryzen\s+3\s+5[34][02]5?U|Ryzen\s+3\s+7330U"), 0.65, "Radeon Vega 6 (6 CU)"),
        (R(@"Ryzen\s+7\s+4[78]00"), 0.95, "Radeon Vega 7/8"),
        (R(@"Ryzen\s+5\s+4[56]00"), 0.8, "Radeon Vega 6"),
        (R(@"Ryzen\s+\d\s+7[35]20U"), 0.45, "Radeon 610M"),
        (R(@"Ryzen\s+\d\s+[23]\d00U"), 0.55, "Radeon Vega (Picasso/Raven)"),
        (R(@"Ryzen\s+\d\s+[4-9]\d00G"), 0.9, "Radeon Vega (desktop APU)"),
    };

    private static Regex R(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static GpuClass? Classify(HardwareInfo hw)
    {
        foreach (var gpu in hw.Gpus)
        {
            foreach (var (pattern, index, igpu) in Table)
            {
                if (pattern.IsMatch(gpu.Name))
                {
                    return new GpuClass(gpu.Name, index, igpu);
                }
            }

            if (gpu.IsIntegrated && gpu.Vendor == "AMD")
            {
                foreach (var (cpu, index, name) in AmdApu)
                {
                    if (cpu.IsMatch(hw.CpuName))
                    {
                        return new GpuClass($"{gpu.Name} ({name})", index, true);
                    }
                }
            }
        }

        return null;
    }
}
