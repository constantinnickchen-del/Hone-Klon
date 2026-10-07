using System.Collections.Generic;
using ConstiBoost.Models;

namespace ConstiBoost.Fortnite;

public sealed record PresetSetting(string Label, string Value);

/// <summary>A Fortnite graphics preset. Values use Unreal Engine scalability levels (0 = Low … 3 = Epic).</summary>
public sealed class FortnitePreset
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string RenderingMode { get; init; } = "Performance";
    public ImpactLevel DefaultImpact { get; init; }
    public int ResolutionIntegrated { get; init; } = 100;
    public int ResolutionDedicated { get; init; } = 100;

    /// <summary>Scalability keys written to GameUserSettings.ini (only if they already exist there).</summary>
    public Dictionary<string, int> Scalability { get; init; } = new();

    public List<PresetSetting> Display { get; init; } = new();

    public static IReadOnlyList<FortnitePreset> All { get; } = new List<FortnitePreset>
    {
        new()
        {
            Id = "maxfps",
            Name = "MAX FPS",
            Subtitle = "For weak PCs and integrated graphics",
            DefaultImpact = ImpactLevel.High,
            ResolutionIntegrated = 75,
            ResolutionDedicated = 100,
            Scalability = new()
            {
                ["sg.ViewDistanceQuality"] = 0, ["sg.AntiAliasingQuality"] = 0, ["sg.ShadowQuality"] = 0,
                ["sg.GlobalIlluminationQuality"] = 0, ["sg.ReflectionQuality"] = 0, ["sg.PostProcessQuality"] = 0,
                ["sg.TextureQuality"] = 0, ["sg.EffectsQuality"] = 0, ["sg.FoliageQuality"] = 0, ["sg.ShadingQuality"] = 0,
            },
            Display = new()
            {
                new("Rendering Mode", "Performance"), new("Graphics", "Low"), new("Shadows", "Off"), new("VSync", "Off"),
                new("Motion Blur", "Off"), new("View Distance", "Low / Near"), new("Effects", "Low"), new("Textures", "Low"),
            },
        },
        new()
        {
            Id = "balanced",
            Name = "BALANCED",
            Subtitle = "Better visibility, still high FPS",
            DefaultImpact = ImpactLevel.Medium,
            ResolutionIntegrated = 85,
            ResolutionDedicated = 100,
            Scalability = new()
            {
                ["sg.ViewDistanceQuality"] = 1, ["sg.AntiAliasingQuality"] = 0, ["sg.ShadowQuality"] = 0,
                ["sg.GlobalIlluminationQuality"] = 0, ["sg.ReflectionQuality"] = 0, ["sg.PostProcessQuality"] = 0,
                ["sg.TextureQuality"] = 1, ["sg.EffectsQuality"] = 0, ["sg.FoliageQuality"] = 0, ["sg.ShadingQuality"] = 0,
            },
            Display = new()
            {
                new("Rendering Mode", "Performance"), new("Textures", "Low/Medium"), new("View Distance", "Medium"),
                new("Shadows", "Off"), new("VSync", "Off"), new("Motion Blur", "Off"), new("Everything else", "Low"),
            },
        },
        new()
        {
            Id = "quality",
            Name = "QUALITY",
            Subtitle = "Higher image quality, good performance",
            RenderingMode = "DirectX 11 (or DirectX 12 on strong GPUs)",
            DefaultImpact = ImpactLevel.Low,
            ResolutionIntegrated = 100,
            ResolutionDedicated = 100,
            Scalability = new()
            {
                ["sg.ViewDistanceQuality"] = 2, ["sg.AntiAliasingQuality"] = 1, ["sg.ShadowQuality"] = 0,
                ["sg.GlobalIlluminationQuality"] = 1, ["sg.ReflectionQuality"] = 1, ["sg.PostProcessQuality"] = 1,
                ["sg.TextureQuality"] = 2, ["sg.EffectsQuality"] = 1, ["sg.FoliageQuality"] = 1, ["sg.ShadingQuality"] = 1,
            },
            Display = new()
            {
                new("Rendering Mode", "DirectX 11/12"), new("Textures", "High"), new("View Distance", "High"),
                new("Effects / Post", "Medium"), new("Shadows", "Off"), new("Lumen / Nanite", "Off"), new("VSync", "Off"),
                new("Motion Blur", "Off"),
            },
        },
    };
}
