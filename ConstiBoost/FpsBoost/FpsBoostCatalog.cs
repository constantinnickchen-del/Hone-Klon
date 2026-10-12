using System.Collections.Generic;
using ConstiBoost.WinSys;

namespace ConstiBoost.FpsBoost;

public static class FpsBoostCatalog
{
    public static List<FpsBoostFeature> Create()
    {
        var list = new List<FpsBoostFeature>
        {
            new PowerSettingFeature("boost-mode", "CPU boost mode: Aggressive", "🚀", "HIGH",
                "Lets the Ryzen CPU reach its full boost clock (up to 4.3 GHz on a 5700U) as soon as a game needs it. Some laptops ship with a reduced or disabled boost – that alone can cost 20-40 % FPS in CPU-limited games.",
                "More heat and fan noise under load (plugged in only). Thermal protection stays fully active.",
                PowerSettings.BoostMode, 2, v => v is 2 or 5, v => v switch
                {
                    0 => "Disabled",
                    1 => "Enabled",
                    2 => "Aggressive",
                    3 => "Efficient enabled",
                    4 => "Efficient aggressive",
                    5 => "Aggressive at guaranteed",
                    6 => "Efficient aggressive at guaranteed",
                    _ => v.ToString(),
                }),
            new PowerSettingFeature("epp", "CPU energy preference: performance", "⚡", "MEDIUM",
                "Tells the CPU to prefer speed over saving energy when plugged in (energy performance preference). The CPU ramps up its clock faster – fewer short FPS drops.",
                "Slightly higher idle temperature and power use when plugged in. Battery values stay unchanged.",
                PowerSettings.EnergyPreference, 10, v => v <= 25, v => v <= 25 ? $"{v} % (performance)" : v >= 75 ? $"{v} % (power saving)" : $"{v} % (balanced)"),
            new PowerSettingFeature("core-parking", "No CPU core parking (plugged in)", "🅿️", "LOW",
                "Keeps all CPU cores awake instead of 'parking' them. Avoids small wake-up delays that can show up as micro-stutter.",
                "A little more idle power use when plugged in. The effect is small on modern Ryzen CPUs – honest note.",
                PowerSettings.CoreParkingMin, 100, v => v >= 100, v => $"min {v} % cores active"),
            new PowerSettingFeature("cooling-policy", "Cooling policy: Active (fan first)", "🌀", "MEDIUM",
                "'Active' means Windows increases the fan speed BEFORE it slows down the CPU. 'Passive' throttles the CPU first – that costs FPS when the laptop gets warm (your PC reaches ~83 °C).",
                "The fan can get louder sooner. This never disables any protection.",
                PowerSettings.CoolingPolicy, 1, v => v == 1, v => v == 1 ? "Active (fan first)" : "Passive (slow down CPU first)"),
            new PowerSettingFeature("switchable-gfx", "Switchable graphics: maximize performance", "🎛️", "HIGH",
                "On laptops with switchable graphics Windows can limit the GPU to save power. 'Maximize performance' lets the GPU run at full speed when plugged in.",
                "More power use while plugged in.",
                PowerSettings.SwitchableGraphics, 3, v => v == 3, v => v switch { 0 => "Force power-saving", 1 => "Optimize power savings", 2 => "Optimize performance", 3 => "Maximize performance", _ => v.ToString() }),
            new PowerSettingFeature("amd-powerplay", "AMD graphics power: maximize performance", "🔺", "MEDIUM",
                "AMD's graphics power setting in the power plan (PowerPlay). 'Maximize performance' keeps the Radeon graphics clock high when plugged in.",
                "More power use and heat while plugged in.",
                PowerSettings.AmdPowerPlay, 2, v => v == 2, v => v switch { 0 => "Maximize battery life", 1 => "Balanced", 2 => "Maximize performance", _ => v.ToString() }),
            new PowerSettingFeature("wifi-power", "Wi-Fi: maximum performance", "📶", "LOW",
                "Stops the Wi-Fi adapter from going into power-saving between packets (plugged in). Fewer ping spikes in online and web games, faster asset loading – not more raw FPS.",
                "Slightly higher power use while plugged in.",
                PowerSettings.WifiPower, 0, v => v == 0, v => v switch { 0 => "Maximum performance", 1 => "Low power saving", 2 => "Medium power saving", 3 => "Maximum power saving", _ => v.ToString() }),
            new PowerSettingFeature("pcie-aspm", "PCI Express power saving off", "🔌", "LOW",
                "Turns off link power saving for PCI Express devices (SSD, Wi-Fi) when plugged in – removes small wake-up latencies (e.g. when a game streams data from the SSD).",
                "Slightly higher power use while plugged in; the FPS effect is small on integrated graphics.",
                PowerSettings.PcieAspm, 0, v => v == 0, v => v switch { 0 => "Off", 1 => "Moderate power savings", 2 => "Maximum power savings", _ => v.ToString() }),

            new WindowedOptimizationsFeature(),
            new BrowserGpuPreferenceFeature(),
            new RefreshRateFeature(),
            new GamingResolutionFeature(),

            new FocusToggleFeature("focus-ecoqos", "No efficiency mode for the game", "🍃", "MEDIUM",
                "Windows 11 can put processes into 'efficiency mode' (EcoQoS): low clock, efficiency cores, lower priority. Browsers use this for tabs. During Game Focus ConstiBoost opts the game (and the game tab) out, so Windows never runs it on efficiency clocks.",
                "None – it only lasts while the game runs.",
                s => s.FocusDisableThrottling, (s, v) => s.FocusDisableThrottling = v),
            new FocusToggleFeature("focus-tabs", "Lower other browser tabs", "🗂️", "MEDIUM",
                "For browser games: during Game Focus all OTHER tabs (renderer processes) get 'Below normal' priority, the game tab gets 'Above normal'. Your other tabs keep working, they just no longer steal CPU time from the game. Extensions (e.g. your ad blocker) are not touched.",
                "Other tabs can react a little slower while you play. Restored on Stop.",
                s => s.FocusLowerOtherTabs, (s, v) => s.FocusLowerOtherTabs = v),
            new FocusToggleFeature("focus-search", "Pause search indexing while playing", "🔎", "LOW",
                "Windows Search indexing can cause disk and CPU spikes (stutter) in the background. During Game Focus the indexing service is paused and started again on Stop. Search still works – new files are indexed afterwards.",
                "Needs a UAC prompt when Focus starts and stops.",
                s => s.FocusPauseSearch, (s, v) => s.FocusPauseSearch = v, SearchAvailability),

            new DeliveryOptimizationFeature(),
            new OverlayDetectorFeature(),
            new CpuHogFeature(),
            new AmdAdrenalinFeature(),
            new DualChannelFeature(),
        };

        for (int i = 0; i < list.Count; i++)
        {
            list[i].Number = i + 1;
        }

        return list;
    }

    private static string? SearchAvailability()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("WSearch");
            _ = sc.Status;
            return null;
        }
        catch
        {
            return "This optimization is unavailable on your system (Windows Search service not installed).";
        }
    }
}
