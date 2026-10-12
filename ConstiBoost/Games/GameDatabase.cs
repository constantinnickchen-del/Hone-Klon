using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConstiBoost.Games;

public sealed record GamePreset(string Name, double Factor);

/// <summary>
/// Reference values for FPS estimates: typical FPS range on a Radeon Vega 8 iGPU (Ryzen 7 5700U class),
/// dual-channel RAM, 1080p, lowest preset. Rough community-typical values – used only for labelled estimates.
/// </summary>
public sealed record GameProfile(string Key, string Name, double LowFps, double HighFps, double CpuCap, IReadOnlyList<GamePreset> Presets, string Tip);

public static class GameDatabase
{
    private static readonly GamePreset[] Standard =
    {
        new("Low", 1.0), new("Medium", 0.72), new("High", 0.52), new("Ultra / Epic", 0.36),
    };

    public static readonly IReadOnlyList<GameProfile> Profiles = new List<GameProfile>
    {
        new("fortnite", "Fortnite", 55, 80, 220, new GamePreset[] { new("Performance Mode · Low", 1.0), new("DirectX 11 · Low", 0.62), new("DirectX 11 · Medium", 0.47), new("DirectX 12 · High", 0.32), new("DirectX 12 · Epic (Lumen)", 0.18) },
            "Performance Mode is by far the best choice on integrated graphics. Cap FPS at your refresh rate for stable frame times and less heat."),
        new("valorant", "VALORANT", 90, 140, 320, Standard, "VALORANT is CPU-heavy – close background apps; 'Limit FPS' helps temperatures a lot."),
        new("cs2", "Counter-Strike 2", 45, 70, 260, Standard, "Use FSR (Performance) on integrated graphics and 'Low' shader detail."),
        new("lol", "League of Legends", 90, 140, 260, Standard, "Very light game – an FPS cap of 60–144 keeps the laptop much cooler."),
        new("minecraft", "Minecraft: Java Edition", 60, 110, 240, Standard, "Lower render distance (8–12 chunks); Sodium/OptiFine help Java Edition a lot. Give Java 2–4 GB RAM in the launcher – more does not help."),
        new("minecraftbedrock", "Minecraft (Bedrock)", 70, 120, 260, Standard, "Bedrock runs much smoother than Java on integrated graphics. Lower render distance and turn off 'Fancy graphics' and 'Beautiful skies'."),
        new("roblox", "Roblox", 60, 100, 240, Standard, "Use graphics level 1–4 manually instead of 'Automatic'."),
        new("rocketleague", "Rocket League", 60, 90, 250, Standard, "Use 'Performance' render quality and lower 3D resolution slightly."),
        new("gta5", "Grand Theft Auto V", 38, 52, 140, Standard, "Lower 'Grass' and 'Population density' first – they are CPU/GPU heavy."),
        new("apex", "Apex Legends", 28, 42, 180, Standard, "Use adaptive resolution FPS target and 'Low' texture streaming budget."),
        new("overwatch", "Overwatch 2", 48, 72, 220, Standard, "Use 'Render scale' 75 % and FSR on integrated graphics."),
        new("warzone", "Call of Duty", 18, 28, 140, Standard, "Very demanding on integrated graphics – use FSR and the lowest preset."),
        new("genshin", "Genshin Impact", 38, 52, 60, Standard, "Genshin caps at 60 FPS – 'Low' preset with render resolution 0.8 is a good start."),
        new("pubg", "PUBG: BATTLEGROUNDS", 25, 40, 160, Standard, "Lower 'View distance' and use 'Very Low' textures."),
        new("dota2", "Dota 2", 60, 90, 220, Standard, "Use 'Fastest' preset with FSR."),
        new("fallguys", "Fall Guys", 40, 60, 200, Standard, "Set resolution scale below 100 % on integrated graphics."),
        new("r6", "Rainbow Six Siege", 55, 80, 240, Standard, "Use render scaling 50–75 % with TAA on integrated graphics."),
        new("eafc", "EA SPORTS FC", 38, 55, 160, Standard, "Lower 'Rendering quality' and 'Grass' first."),
        new("sims4", "The Sims 4", 45, 70, 140, Standard, "Use 'Laptop mode' in graphics settings."),
        new("eldenring", "Elden Ring", 18, 26, 60, Standard, "Very demanding – 720p and the lowest preset are realistic on integrated graphics."),
        new("cyberpunk", "Cyberpunk 2077", 15, 22, 120, Standard, "Use the 'Steam Deck' preset and FSR Performance."),
        new("amongus", "Among Us", 120, 200, 300, Standard, "Very light – cap FPS to keep the laptop cool."),
        new("brawlhalla", "Brawlhalla", 120, 200, 300, Standard, "Very light – cap FPS to keep the laptop cool."),
    };

    private static readonly (Regex pattern, string key)[] Matchers =
    {
        (R(@"^Fortnite$"), "fortnite"), (R(@"VALORANT"), "valorant"), (R(@"Counter-Strike 2|^CS2$"), "cs2"), (R(@"League of Legends"), "lol"),
        (R(@"Minecraft.*(Bedrock|for Windows|Preview|Education)|Minecraft \(Bedrock"), "minecraftbedrock"), (R(@"Minecraft"), "minecraft"), (R(@"Roblox"), "roblox"), (R(@"Rocket League"), "rocketleague"), (R(@"Grand Theft Auto V|GTA V"), "gta5"),
        (R(@"Apex Legends"), "apex"), (R(@"Overwatch"), "overwatch"), (R(@"Call of Duty|Warzone"), "warzone"), (R(@"Genshin"), "genshin"),
        (R(@"PUBG"), "pubg"), (R(@"Dota 2"), "dota2"), (R(@"Fall Guys"), "fallguys"), (R(@"Rainbow Six"), "r6"), (R(@"EA SPORTS FC|FIFA \d"), "eafc"),
        (R(@"The Sims 4"), "sims4"), (R(@"ELDEN RING"), "eldenring"), (R(@"Cyberpunk 2077"), "cyberpunk"), (R(@"Among Us"), "amongus"), (R(@"Brawlhalla"), "brawlhalla"),
    };

    private static readonly Dictionary<string, string> SteamIds = new()
    {
        ["730"] = "cs2", ["570"] = "dota2", ["271590"] = "gta5", ["1172470"] = "apex", ["252950"] = "rocketleague", ["578080"] = "pubg",
        ["2357570"] = "overwatch", ["1097150"] = "fallguys", ["359550"] = "r6", ["1245620"] = "eldenring", ["1091500"] = "cyberpunk", ["945360"] = "amongus", ["291550"] = "brawlhalla",
    };

    private static Regex R(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? Match(string name, string? steamAppId, string? epicAppName)
    {
        if (string.Equals(epicAppName, "Fortnite", StringComparison.OrdinalIgnoreCase))
        {
            return "fortnite";
        }

        if (steamAppId != null && SteamIds.TryGetValue(steamAppId, out string? key))
        {
            return key;
        }

        return Matchers.FirstOrDefault(m => m.pattern.IsMatch(name)).key;
    }

    public static GameProfile? Get(string? key) => key is null ? null : Profiles.FirstOrDefault(p => p.Key == key);
}
