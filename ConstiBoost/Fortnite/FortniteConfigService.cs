using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;

namespace ConstiBoost.Fortnite;

public sealed class FortniteConfigSnapshot
{
    public bool Exists { get; init; }
    public string Path { get; init; } = string.Empty;
    public Dictionary<string, string> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string RenderingMode
    {
        get
        {
            Values.TryGetValue("PreferredRHI", out string? rhi);
            Values.TryGetValue("PreferredFeatureLevel", out string? level);
            if (string.Equals(level, "es31", StringComparison.OrdinalIgnoreCase))
            {
                return "Performance";
            }

            return rhi?.ToLowerInvariant() switch
            {
                "dx11" => "DirectX 11",
                "dx12" => "DirectX 12",
                null => Formatting.Unavailable,
                _ => rhi,
            };
        }
    }

    public string Get(string key) => Values.TryGetValue(key, out string? v) ? v : "–";
}

public sealed record PresetOptions(int ResolutionPercent, bool DisableVSync, bool DisableMotionBlur, int? FrameRateLimit, bool Fullscreen);

/// <summary>
/// Reads and writes Fortnite's own settings file:
/// %LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini
/// Only keys that already exist in the file are changed. The file is backed up first, and Fortnite must be closed.
/// </summary>
public static class FortniteConfigService
{
    private static readonly string[] TrackedKeys =
    {
        "sg.ResolutionQuality", "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality",
        "sg.GlobalIlluminationQuality", "sg.ReflectionQuality", "sg.PostProcessQuality", "sg.TextureQuality",
        "sg.EffectsQuality", "sg.FoliageQuality", "sg.ShadingQuality", "bUseVSync", "bMotionBlur", "FrameRateLimit",
        "FullscreenMode", "LastConfirmedFullscreenMode", "PreferredFullscreenMode", "ResolutionSizeX", "ResolutionSizeY",
        "PreferredRHI", "PreferredFeatureLevel",
    };

    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");

    public static FortniteConfigSnapshot Read()
    {
        string path = ConfigPath;
        if (!File.Exists(path))
        {
            return new FortniteConfigSnapshot { Exists = false, Path = path };
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ini = IniDocument.Load(path);
            foreach (string key in TrackedKeys)
            {
                string? v = ini.Get(key);
                if (v != null)
                {
                    values[key] = v;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Fortnite config unreadable: " + ex.Message);
        }

        return new FortniteConfigSnapshot { Exists = true, Path = path, Values = values };
    }

    public static bool IsFortniteRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(FortniteLocator.ProcessName);
            bool running = processes.Length > 0;
            foreach (var p in processes)
            {
                p.Dispose();
            }

            return running;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Estimates how strongly a preset lowers the current settings. No FPS numbers are claimed.</summary>
    public static ImpactLevel EstimateImpact(FortnitePreset preset, FortniteConfigSnapshot snapshot, int resolutionPercent)
    {
        if (!snapshot.Exists || snapshot.Values.Count == 0)
        {
            return preset.DefaultImpact;
        }

        double delta = 0;
        foreach (var (key, target) in preset.Scalability)
        {
            if (snapshot.Values.TryGetValue(key, out string? raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double current))
            {
                delta += Math.Max(0, current - target);
            }
        }

        if (snapshot.Values.TryGetValue("sg.ResolutionQuality", out string? res) &&
            double.TryParse(res, NumberStyles.Float, CultureInfo.InvariantCulture, out double currentRes))
        {
            delta += Math.Max(0, currentRes - resolutionPercent) / 8.0;
        }

        if (snapshot.RenderingMode == "DirectX 12" && preset.RenderingMode == "Performance")
        {
            delta += 6;
        }

        return delta <= 2 ? ImpactLevel.Low : delta <= 8 ? ImpactLevel.Medium : ImpactLevel.High;
    }

    public static (OperationResult result, List<string> missingKeys) Apply(FortnitePreset preset, PresetOptions options, BackupSession backup)
    {
        var missing = new List<string>();
        string path = ConfigPath;
        if (!File.Exists(path))
        {
            return (OperationResult.Unavailable("Fortnite's settings file was not found. Start Fortnite once, then try again."), missing);
        }

        if (IsFortniteRunning())
        {
            return (OperationResult.Fail("Please close Fortnite first – it overwrites its settings file when it exits."), missing);
        }

        try
        {
            backup.CaptureFile(path, "Fortnite GameUserSettings.ini");
            var ini = IniDocument.Load(path);
            var changes = new List<(string key, string old, string value)>();

            void Write(string key, string value)
            {
                string? old = ini.Get(key);
                if (old is null)
                {
                    missing.Add(key);
                    return;
                }

                if (!string.Equals(old, value, StringComparison.OrdinalIgnoreCase))
                {
                    ini.Set(key, value);
                    changes.Add((key, old, value));
                }
            }

            foreach (var (key, value) in preset.Scalability)
            {
                Write(key, value.ToString(CultureInfo.InvariantCulture));
            }

            Write("sg.ResolutionQuality", options.ResolutionPercent.ToString("0.000000", CultureInfo.InvariantCulture));
            if (options.DisableVSync)
            {
                Write("bUseVSync", "False");
            }

            if (options.DisableMotionBlur)
            {
                Write("bMotionBlur", "False");
            }

            if (options.FrameRateLimit is int limit)
            {
                Write("FrameRateLimit", limit.ToString("0.000000", CultureInfo.InvariantCulture));
            }

            if (options.Fullscreen)
            {
                Write("FullscreenMode", "0");
                Write("LastConfirmedFullscreenMode", "0");
                Write("PreferredFullscreenMode", "0");
            }

            ini.Save(path);
            foreach (var (key, old, value) in changes)
            {
                ActionLog.Success($"Fortnite {preset.Name}: {key}", old, value);
            }

            ActionLog.Success($"Fortnite preset {preset.Name}", "previous settings", $"{changes.Count} value(s) changed");
            return (OperationResult.Ok($"{preset.Name} applied: {changes.Count} setting(s) changed."), missing);
        }
        catch (Exception ex)
        {
            ActionLog.Failed($"Fortnite preset {preset.Name}", null, null, Safe.Describe(ex));
            AppLog.Error("Fortnite preset failed", ex);
            return (OperationResult.Fail("The preset couldn't be applied.", Safe.Describe(ex)), missing);
        }
    }
}
