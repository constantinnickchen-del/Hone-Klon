using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Fortnite;
using ConstiBoost.Logging;

namespace ConstiBoost.Games;

public sealed record ConfigValue(string Label, string Value);

public sealed record ConfigPreset(string Name, string Description);

/// <summary>Edits a game's own settings file. Only existing keys are changed; the file is backed up first; the game must be closed.</summary>
public interface IGameConfigEditor
{
    string Title { get; }
    string FileInfo { get; }
    bool IsAvailable { get; }
    IReadOnlyList<ConfigValue> ReadCurrent();
    IReadOnlyList<ConfigPreset> Presets { get; }
    OperationResult Apply(string presetName, int? fpsCap, BackupSession backup);
}

public static class GameConfigEditors
{
    /// <summary>Returns an editor for the game, or null (Fortnite has its own dedicated tools).</summary>
    public static IGameConfigEditor? For(GameInfo game)
    {
        if (game.IsFortnite)
        {
            return null;
        }

        if (game.Source == GameSource.Minecraft)
        {
            var mc = new MinecraftOptionsEditor(Path.Combine(game.InstallPath, "options.txt"));
            return mc.IsAvailable ? mc : null;
        }

        var ue = UnrealConfigEditor.Find(game);
        return ue != null && ue.IsAvailable ? ue : null;
    }
}

/// <summary>Minecraft: Java Edition options.txt ("key:value" lines).</summary>
public sealed class MinecraftOptionsEditor : IGameConfigEditor
{
    private readonly string _path;

    public MinecraftOptionsEditor(string path) => _path = path;

    public string Title => "Minecraft video settings";
    public string FileInfo => _path;
    public bool IsAvailable => File.Exists(_path);

    public IReadOnlyList<ConfigPreset> Presets { get; } = new List<ConfigPreset>
    {
        new("LOW-END", "Render distance 8, simulation 6, Fast graphics, minimal particles, no clouds/shadows/mipmaps, VSync off"),
        new("BALANCED", "Render distance 12, simulation 8, Fast graphics, fast clouds, smooth lighting on"),
        new("QUALITY", "Render distance 16, simulation 10, Fancy graphics, all particles, entity shadows"),
    };

    private static readonly (string key, string label)[] Shown =
    {
        ("renderDistance", "Render distance"), ("simulationDistance", "Simulation distance"), ("graphicsMode", "Graphics (0 Fast, 1 Fancy, 2 Fabulous)"),
        ("fancyGraphics", "Fancy graphics"), ("maxFps", "Max FPS (260 = unlimited)"), ("enableVsync", "VSync"), ("particles", "Particles (0 all, 2 minimal)"),
        ("ao", "Smooth lighting"), ("entityShadows", "Entity shadows"), ("renderClouds", "Clouds"), ("mipmapLevels", "Mipmap levels"), ("biomeBlendRadius", "Biome blend"),
    };

    private Dictionary<string, string> Read(out List<string> lines)
    {
        lines = File.ReadAllLines(_path, Encoding.UTF8).ToList();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            int idx = line.IndexOf(':');
            if (idx > 0)
            {
                values[line[..idx]] = line[(idx + 1)..];
            }
        }

        return values;
    }

    public IReadOnlyList<ConfigValue> ReadCurrent()
    {
        try
        {
            var values = Read(out _);
            return Shown.Where(s => values.ContainsKey(s.key)).Select(s => new ConfigValue(s.label, values[s.key])).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Warn("options.txt unreadable: " + ex.Message);
            return Array.Empty<ConfigValue>();
        }
    }

    public OperationResult Apply(string presetName, int? fpsCap, BackupSession backup)
    {
        try
        {
            var values = Read(out var lines);
            bool low = presetName == "LOW-END";
            bool quality = presetName == "QUALITY";
            var target = new Dictionary<string, object>
            {
                ["renderDistance"] = low ? 8 : quality ? 16 : 12,
                ["simulationDistance"] = low ? 6 : quality ? 10 : 8,
                ["graphicsMode"] = quality ? 1 : 0,
                ["fancyGraphics"] = quality,
                ["enableVsync"] = false,
                ["particles"] = low ? 2 : quality ? 0 : 1,
                ["entityShadows"] = quality,
                ["renderClouds"] = low ? "\"false\"" : quality ? "\"true\"" : "\"fast\"",
                ["mipmapLevels"] = low ? 0 : quality ? 4 : 2,
                ["biomeBlendRadius"] = low ? 0 : quality ? 2 : 1,
                ["ao"] = !low,
            };
            if (fpsCap is int cap)
            {
                target["maxFps"] = Math.Clamp(cap, 10, 260);
            }

            backup.CaptureFile(_path, "Minecraft options.txt");
            int changed = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int idx = lines[i].IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                string key = lines[i][..idx];
                if (!target.TryGetValue(key, out object? wanted))
                {
                    continue;
                }

                string old = lines[i][(idx + 1)..];
                string value = wanted switch
                {
                    bool b when key == "ao" && int.TryParse(old, out _) => b ? "2" : "0", // older versions: 0/1/2
                    bool b => b ? "true" : "false",
                    int n => n.ToString(CultureInfo.InvariantCulture),
                    _ => old.StartsWith('"') ? wanted.ToString()! : wanted.ToString()!.Trim('"'),
                };
                if (old != value)
                {
                    lines[i] = $"{key}:{value}";
                    ActionLog.Success($"Minecraft {presetName}: {key}", old, value);
                    changed++;
                }
            }

            File.WriteAllLines(_path, lines, new UTF8Encoding(false));
            return OperationResult.Ok($"{presetName}: {changed} Minecraft setting(s) changed.");
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Minecraft settings", null, presetName, Safe.Describe(ex));
            return OperationResult.Fail("The Minecraft settings couldn't be changed.", Safe.Describe(ex));
        }
    }
}

/// <summary>
/// Generic Unreal Engine GameUserSettings.ini editor (VALORANT, PUBG and many other UE games).
/// Changes only the standard scalability keys (sg.*), VSync and the frame rate limit – only if they exist.
/// </summary>
public sealed class UnrealConfigEditor : IGameConfigEditor
{
    private static readonly Dictionary<string, string> KnownProjects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VALORANT"] = "VALORANT", ["PUBG: BATTLEGROUNDS"] = "TslGame", ["PUBG"] = "TslGame", ["Rocket League"] = string.Empty,
    };

    private static readonly string[] QualityKeys =
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.GlobalIlluminationQuality", "sg.ReflectionQuality",
        "sg.PostProcessQuality", "sg.TextureQuality", "sg.EffectsQuality", "sg.FoliageQuality", "sg.ShadingQuality",
    };

    private readonly List<string> _files;

    private UnrealConfigEditor(List<string> files) => _files = files;

    public string Title => "Unreal Engine graphics settings";
    public string FileInfo => string.Join("\n", _files);
    public bool IsAvailable => _files.Count > 0;

    public IReadOnlyList<ConfigPreset> Presets { get; } = new List<ConfigPreset>
    {
        new("LOW-END", "All scalability groups Low, VSync off"),
        new("BALANCED", "All scalability groups Medium, shadows Low, VSync off"),
        new("QUALITY", "All scalability groups High, VSync off"),
    };

    public static UnrealConfigEditor? Find(GameInfo game)
    {
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (KnownProjects.TryGetValue(game.Name, out string? known))
        {
            if (known.Length == 0)
            {
                return null;
            }

            projects.Add(known);
        }

        if (game.EpicLaunchExecutable?.Replace('\\', '/').Split('/').FirstOrDefault() is { Length: > 0 } epicProject)
        {
            projects.Add(epicProject);
        }

        if (game.MainExecutable is { } exe)
        {
            var parts = exe.Split('\\');
            int bin = Array.FindIndex(parts, p => p.Equals("Binaries", StringComparison.OrdinalIgnoreCase));
            if (bin > 0)
            {
                projects.Add(parts[bin - 1]);
            }
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var files = new List<string>();
        foreach (string project in projects)
        {
            string config = Path.Combine(local, project, "Saved", "Config");
            if (!Directory.Exists(config))
            {
                continue;
            }

            try
            {
                files.AddRange(Directory.EnumerateFiles(config, "GameUserSettings.ini", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 }));
            }
            catch
            {
                // ignore
            }
        }

        files = files.Where(f => File.ReadAllText(f).Contains("sg.", StringComparison.Ordinal)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return files.Count == 0 ? null : new UnrealConfigEditor(files);
    }

    public IReadOnlyList<ConfigValue> ReadCurrent()
    {
        try
        {
            var ini = IniDocument.Load(_files[0]);
            var list = QualityKeys.Select(k => (k, v: ini.Get(k))).Where(x => x.v != null).Select(x => new ConfigValue(x.k.Replace("sg.", string.Empty).Replace("Quality", string.Empty), Level(x.v!))).ToList();
            if (ini.Get("bUseVSync") is { } vsync) list.Add(new ConfigValue("VSync", vsync));
            if (ini.Get("FrameRateLimit") is { } fps) list.Add(new ConfigValue("Frame rate limit", fps.StartsWith('0') ? "Unlimited" : fps.Split('.')[0]));
            if (ini.Get("sg.ResolutionQuality") is { } res) list.Add(new ConfigValue("3D resolution", res.Split('.')[0] + " %"));
            return list;
        }
        catch (Exception ex)
        {
            AppLog.Warn("GameUserSettings unreadable: " + ex.Message);
            return Array.Empty<ConfigValue>();
        }
    }

    private static string Level(string v) => v switch { "0" => "Low", "1" => "Medium", "2" => "High", "3" => "Epic", "4" => "Cinematic", _ => v };

    public OperationResult Apply(string presetName, int? fpsCap, BackupSession backup)
    {
        int level = presetName == "LOW-END" ? 0 : presetName == "QUALITY" ? 2 : 1;
        int changedTotal = 0;
        try
        {
            foreach (string file in _files)
            {
                backup.CaptureFile(file, "GameUserSettings.ini");
                var ini = IniDocument.Load(file);
                foreach (string key in QualityKeys)
                {
                    int value = key == "sg.ShadowQuality" && level == 1 ? 0 : level;
                    string? old = ini.Get(key);
                    if (old != null && old != value.ToString(CultureInfo.InvariantCulture))
                    {
                        ini.Set(key, value.ToString(CultureInfo.InvariantCulture));
                        ActionLog.Success($"{presetName}: {key}", old, value.ToString(CultureInfo.InvariantCulture));
                        changedTotal++;
                    }
                }

                if (ini.Get("bUseVSync") is { } vs && !vs.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    ini.Set("bUseVSync", "False");
                    changedTotal++;
                }

                if (fpsCap is int cap && ini.Has("FrameRateLimit"))
                {
                    ini.Set("FrameRateLimit", cap.ToString("0.000000", CultureInfo.InvariantCulture));
                    changedTotal++;
                }

                ini.Save(file);
            }

            return OperationResult.Ok($"{presetName}: {changedTotal} value(s) changed in {_files.Count} settings file(s). Some games re-apply in-game settings from the cloud – check the in-game menu once.");
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Unreal settings", null, presetName, Safe.Describe(ex));
            return OperationResult.Fail("The settings couldn't be changed.", Safe.Describe(ex));
        }
    }
}
