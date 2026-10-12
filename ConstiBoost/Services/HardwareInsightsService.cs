using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Localization;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed record UpgradeTip(string Title, string WhatToGet, string Why, string Effect, ImpactLevel Priority)
{
    // Fixed English texts are translated here; texts with values use L.F where they are built.
    public string Title { get; init; } = L.T(Title);
    public string WhatToGet { get; init; } = L.T(WhatToGet);
    public string Why { get; init; } = L.T(Why);
    public string Effect { get; init; } = L.T(Effect);

    public string PriorityText => L.T(Priority switch { ImpactLevel.High => "BEST UPGRADE", ImpactLevel.Medium => "WORTH IT", _ => "OPTIONAL" });
}

public sealed record HardwareInsight(string Title, CheckStatus Status, string Current, string Detail, string Recommendation, ImpactLevel Impact)
{
    public string Title { get; init; } = L.T(Title);
    public string Current { get; init; } = L.T(Current);
    public string Detail { get; init; } = L.T(Detail);
    public string Recommendation { get; init; } = L.T(Recommendation);

    public string ImpactText => L.T(Impact.ToString().ToUpperInvariant());
}

/// <summary>
/// "Your PC can do more than you think": finds real hardware/configuration bottlenecks that matter most
/// on low-end gaming PCs. Read-only – it only explains, it never changes BIOS or hardware settings.
/// </summary>
public static class HardwareInsightsService
{
    /// <summary>Concrete, honest upgrade advice based on the real slots/modules/drives of this PC.</summary>
    public static List<UpgradeTip> UpgradeAdvice(HardwareInfo hw, IEnumerable<string> gameDrives)
    {
        var tips = new List<UpgradeTip>();
        var layout = MemoryInfo.Read();
        bool igpu = hw.Gpus.FirstOrDefault()?.IsIntegrated == true;
        var first = layout.Modules.FirstOrDefault();
        string spec = first is null ? L.T("matching module") : $"{first.Type} {(first.SpeedMts?.ToString() ?? "")} MT/s {first.FormFactor}".Replace("  ", " ");
        long totalGb = (hw.TotalRamBytes ?? 0) / 1024 / 1024 / 1024;

        if (layout.Modules.Count == 1 && layout.FreeSlots is > 0)
        {
            string size = first != null ? $"{first.CapacityBytes / 1024 / 1024 / 1024} GB" : L.T("same size");
            tips.Add(new UpgradeTip("Add a second RAM module (dual-channel)",
                $"1× {size} {spec}" + (first is { PartNumber.Length: > 0 } ? L.F(" – ideally the same model as installed ({0} {1})", first.Manufacturer, first.PartNumber) : string.Empty),
                L.F("{0} free slot detected and only one module installed. Dual-channel doubles memory bandwidth.", layout.FreeSlots),
                igpu ? "Typically the biggest FPS gain for integrated graphics (often 20–40 % more FPS in GPU-limited games, according to common test results). Also more RAM for the game." : "Smoother frame times and more RAM.",
                igpu ? ImpactLevel.High : ImpactLevel.Medium));
        }
        else if (layout.Modules.Count == 1 && layout.FreeSlots == 0)
        {
            tips.Add(new UpgradeTip("RAM is single-channel, but no free slot was detected",
                "Check your laptop's manual – some models have soldered memory plus one slot that Windows reports differently.",
                "Only one memory module is reported.", "If a slot exists, a second module is the best upgrade for integrated graphics.", ImpactLevel.Medium));
        }

        if (totalGb is > 0 and < 12 && layout.FreeSlots is > 0 && layout.Modules.Count != 1)
        {
            tips.Add(new UpgradeTip("More RAM (16 GB total)", L.F("Add a {0} module so that the total reaches 16 GB", spec),
                L.F("{0} GB installed – modern games plus Windows and the iGPU use most of it.", totalGb), "Fewer stutters when switching areas or with Discord/browser open.", ImpactLevel.Medium));
        }

        var media = StorageInfo.Read();
        foreach (string d in gameDrives.Select(x => x.TrimEnd('\\').ToUpperInvariant()).Distinct())
        {
            if (media.FirstOrDefault(m => m.Letter.Equals(d, StringComparison.OrdinalIgnoreCase)) is { MediaType: "HDD" })
            {
                tips.Add(new UpgradeTip(L.F("SSD for your games (drive {0} is a hard disk)", d), "A SATA or NVMe SSD (check which your laptop supports)",
                    "Games installed on a hard disk.", "Much faster loading and fewer texture-streaming stutters – no FPS change in already-loaded scenes.", ImpactLevel.High));
            }
        }

        if (hw.HasBattery)
        {
            tips.Add(new UpgradeTip("Laptop cooling pad (cheap, optional)", "A simple cooling pad or a stand that lifts the back",
                "Laptops pull air from the bottom – more airflow = lower temperatures.", "Lower temperatures can keep boost clocks higher for longer. Effect depends on the laptop.", ImpactLevel.Low));
        }

        if (tips.Count == 0)
        {
            tips.Add(new UpgradeTip("No obvious cheap upgrade", "Your memory and storage are already well configured.", "Dual-channel memory and SSD detected.", "Further gains would need a faster CPU/GPU.", ImpactLevel.Low));
        }

        return tips;
    }

    public static List<HardwareInsight> Analyze(HardwareInfo hw, IEnumerable<string> gameDrives)
    {
        var list = new List<HardwareInsight>();
        var gpu = hw.Gpus.FirstOrDefault();
        bool igpu = gpu?.IsIntegrated == true && !hw.Gpus.Any(g => !g.IsIntegrated && g.Vendor is "NVIDIA" or "AMD" or "Intel" && !g.Name.Contains("Basic", StringComparison.OrdinalIgnoreCase));
        var gpuClass = GpuDatabase.Classify(hw);

        // 1. Memory channels – the biggest lever for integrated graphics
        if (hw.RamModules is int modules)
        {
            list.Add(modules == 1
                ? new HardwareInsight("Single-channel memory", igpu ? CheckStatus.Bad : CheckStatus.Warning, "1 memory module detected",
                    "Integrated graphics use system RAM as video memory. With only one module the memory bandwidth is halved, which can significantly reduce FPS on integrated graphics.",
                    "If your laptop has a free RAM slot, a second module with the same size and speed enables dual-channel – often the biggest FPS upgrade for iGPU laptops. Check your model's manual first (some models have soldered RAM plus one slot).",
                    igpu ? ImpactLevel.High : ImpactLevel.Medium)
                : new HardwareInsight("Dual-channel memory", CheckStatus.Good, L.F("{0} memory modules", modules), "Memory bandwidth is fully used – ideal for integrated graphics.", "Nothing to do.", ImpactLevel.Low));
        }

        // 2. Memory size
        if (hw.TotalRamBytes is long ram)
        {
            double gb = ram / 1024d / 1024 / 1024;
            list.Add(gb < 7
                ? new HardwareInsight("Little RAM", CheckStatus.Bad, $"{gb:0.#} GB", "Games, Windows and the iGPU share this memory. Below 8 GB modern games stutter when memory runs out.", "Close browsers and launchers before gaming (Game Focus does this) – an upgrade to 16 GB helps a lot.", ImpactLevel.High)
                : gb < 12
                    ? new HardwareInsight("8 GB RAM", igpu ? CheckStatus.Warning : CheckStatus.Good, $"{gb:0.#} GB", igpu ? "Part of the 8 GB is reserved for the integrated GPU, so free memory gets tight in modern games." : "Enough for most games.", "Close background apps while gaming; 16 GB is the comfortable size.", ImpactLevel.Medium)
                    : new HardwareInsight("Enough RAM", CheckStatus.Good, $"{gb:0.#} GB", "Plenty of memory for games and the integrated GPU.", "Nothing to do.", ImpactLevel.Low));
        }

        // 3. Memory speed
        if (hw.RamSpeedMhz is int speed && igpu)
        {
            list.Add(speed < 2666
                ? new HardwareInsight("Slow memory", CheckStatus.Warning, $"{speed} MT/s", "Memory speed directly affects integrated graphics performance.", "When upgrading RAM choose the fastest speed your laptop supports (DDR4-3200 for Ryzen 5000 laptops).", ImpactLevel.Medium)
                : new HardwareInsight("Memory speed", CheckStatus.Good, $"{speed} MT/s", "Good speed for integrated graphics.", "Nothing to do.", ImpactLevel.Low));
        }

        // 4. Reserved video memory (UMA frame buffer)
        if (igpu && gpu?.AdapterRamBytes is long vram)
        {
            double mb = vram / 1024d / 1024;
            list.Add(new HardwareInsight("Reserved video memory (UMA)", mb <= 512 ? CheckStatus.Warning : CheckStatus.Good, L.F("{0:0} MB dedicated", mb),
                "Windows gives the iGPU more shared memory automatically when needed, but some games refuse higher textures with a small dedicated value.",
                mb <= 512 ? "Some BIOS versions offer 'UMA Frame Buffer Size' – 1–2 GB can help in VRAM-hungry games. Only change it if your BIOS offers this option; ConstiBoost never changes BIOS settings." : "Nothing to do.",
                ImpactLevel.Low));
        }

        // 5. GPU driver age
        if (gpu?.DriverDate is DateTime date)
        {
            int days = (int)(DateTime.Now - date).TotalDays;
            string where = gpu.Vendor switch
            {
                "AMD" => "AMD Software: Adrenalin Edition from amd.com/support (for Ryzen laptops the AMD driver usually has newer game optimizations than the laptop maker's driver)",
                "NVIDIA" => "NVIDIA App / GeForce drivers from nvidia.com",
                "Intel" => "Intel Driver & Support Assistant",
                _ => "the GPU vendor's website",
            };
            list.Add(days > 365
                ? new HardwareInsight("Old graphics driver", CheckStatus.Bad, L.F("{0:dd.MM.yyyy} ({1} months)", date, days / 30), "Old drivers miss game-specific optimizations and fixes – especially for new games like Fortnite seasons.", L.F("Update via {0}.", L.T(where)), ImpactLevel.High)
                : days > 180
                    ? new HardwareInsight("Graphics driver could be newer", CheckStatus.Warning, $"{date:dd.MM.yyyy}", "Newer drivers often improve performance in current games.", L.F("Check {0}.", L.T(where)), ImpactLevel.Medium)
                    : new HardwareInsight("Graphics driver up to date", CheckStatus.Good, $"{date:dd.MM.yyyy}", "Recent driver.", "Nothing to do.", ImpactLevel.Low));
        }

        // 6. Storage type of game drives
        var media = StorageInfo.Read();
        var drives = gameDrives.Append(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\")
            .Select(d => d.TrimEnd('\\').ToUpperInvariant()).Distinct();
        foreach (string d in drives)
        {
            var m = media.FirstOrDefault(x => x.Letter.Equals(d, StringComparison.OrdinalIgnoreCase));
            if (m is null)
            {
                continue;
            }

            list.Add(m.MediaType == "HDD"
                ? new HardwareInsight(L.F("Drive {0} is a hard disk (HDD)", d), CheckStatus.Bad, "HDD", "Games on a hard disk load much slower and can stutter when new areas/textures stream in.", "Install your main games on an SSD.", ImpactLevel.High)
                : new HardwareInsight(L.F("Drive {0}: {1}", d, m.MediaType + (m.BusType == "NVMe" ? " (NVMe)" : string.Empty)), m.MediaType == "SSD" ? CheckStatus.Good : CheckStatus.Unavailable, m.MediaType, m.MediaType == "SSD" ? "Fast storage – good for loading times." : "Drive type could not be determined.", "Nothing to do.", ImpactLevel.Low));
        }

        // 7. Display
        if (gpu?.RefreshRate is int hz)
        {
            list.Add(new HardwareInsight("Display refresh rate", CheckStatus.Good, $"{hz} Hz · {gpu.HorizontalResolution}×{gpu.VerticalResolution}",
                L.F("Your screen shows at most {0} images per second. More FPS than that lower input lag slightly but you will not see them.", hz),
                L.F("Cap FPS at {0} (or {1} for competitive play) – stable frame times, less heat, longer battery life.", hz, hz * 2), ImpactLevel.Medium));
        }

        // 8. Power source
        if (hw.HasBattery)
        {
            bool? ac = SystemInfo.GetPowerStatus().onAc;
            list.Add(ac == false
                ? new HardwareInsight("Running on battery", CheckStatus.Warning, L.F("Battery {0} %", hw.BatteryPercent?.ToString() ?? "?"), "Laptops limit CPU/GPU power on battery – often a large FPS loss.", "Plug in the charger for gaming.", ImpactLevel.High)
                : new HardwareInsight("Plugged in", CheckStatus.Good, "AC power", "Full power available.", "Nothing to do.", ImpactLevel.Low));
        }

        // 9. Graphics class summary
        if (gpuClass != null)
        {
            list.Add(new HardwareInsight("Graphics performance class", CheckStatus.Good, gpuClass.Name, L.F("Approximate performance class {0:0.00}× a Radeon Vega 8 iGPU (used for FPS estimates in Games).", gpuClass.Index),
                gpuClass.Integrated ? "Performance modes, lower 3D resolution and FPS caps give the biggest gains on integrated graphics." : "Dedicated graphics – keep the laptop plugged in and drivers current.", ImpactLevel.Low));
        }

        return list.OrderBy(i => i.Status switch { CheckStatus.Bad => 0, CheckStatus.Warning => 1, CheckStatus.Good => 3, _ => 2 })
            .ThenByDescending(i => i.Impact).ToList();
    }
}
