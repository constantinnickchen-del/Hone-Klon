using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Games;

/// <summary>
/// Minecraft: Java Edition via the Minecraft Launcher (Store or standalone). The game itself runs in javaw.exe from
/// the launcher's Java runtime, so those runtime folders are added as process roots.
/// </summary>
public static class MinecraftScanner
{
    public const string LauncherFamily = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";

    public static string DotMinecraft => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

    public static GameInfo? ScanJava(List<StorePackage> packages)
    {
        try
        {
            string data = DotMinecraft;
            var storeLauncher = packages.FirstOrDefault(p => p.FamilyName.Equals(LauncherFamily, StringComparison.OrdinalIgnoreCase));
            string standaloneDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Minecraft Launcher");
            string standaloneExe = Path.Combine(standaloneDir, "MinecraftLauncher.exe");
            bool standalone = File.Exists(standaloneExe);
            if (!Directory.Exists(data) && storeLauncher is null && !standalone)
            {
                return null;
            }

            var runtimeRoots = new List<string>
            {
                Path.Combine(data, "runtime"),
                Path.Combine(standaloneDir, "runtime"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", LauncherFamily, "LocalCache", "Local", "runtime"),
            }.Where(Directory.Exists).ToList();

            string? javaw = runtimeRoots.SelectMany(r => SafeFind(r, "javaw.exe")).FirstOrDefault();
            string? aumid = null;
            if (storeLauncher != null)
            {
                var (appId, _) = StoreGamesScanner.ReadManifest(storeLauncher.RootFolder);
                aumid = appId is null ? null : $"{storeLauncher.FamilyName}!{appId}";
            }

            var game = new GameInfo
            {
                Id = "minecraft:java",
                Name = "Minecraft: Java Edition",
                Source = GameSource.Minecraft,
                Publisher = "Mojang",
                InstallPath = Directory.Exists(data) ? data : standaloneDir,
                ExtraProcessRoots = runtimeRoots,
                MainExecutable = javaw,
                ExecutableNames = new List<string> { "javaw" },
                AppUserModelId = aumid,
                PackageFullName = storeLauncher?.FullName,
                LaunchUri = aumid is null && standalone ? standaloneExe : null,
                ProfileKey = "minecraft",
            };

            ApplyJavaLastPlayed(game, data);
            return game;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Minecraft Java scan failed: " + ex.Message);
            return null;
        }
    }

    /// <summary>Real last-played data: launcher_profiles.json "lastUsed" and logs\latest.log.</summary>
    private static void ApplyJavaLastPlayed(GameInfo game, string data)
    {
        DateTime? best = null;
        string source = string.Empty;
        try
        {
            string profiles = Path.Combine(data, "launcher_profiles.json");
            if (File.Exists(profiles))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(profiles));
                if (doc.RootElement.TryGetProperty("profiles", out var list))
                {
                    foreach (var profile in list.EnumerateObject())
                    {
                        if (profile.Value.TryGetProperty("lastUsed", out var used) && used.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(used.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) &&
                            t.Year > 2000 && (best is null || t.ToLocalTime() > best))
                        {
                            best = t.ToLocalTime();
                            source = "Minecraft Launcher";
                        }
                    }
                }
            }

            string log = Path.Combine(data, "logs", "latest.log");
            if (File.Exists(log) && (best is null || File.GetLastWriteTime(log) > best))
            {
                best = File.GetLastWriteTime(log);
                source = "game log";
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Minecraft last played unreadable: " + ex.Message);
        }

        if (best != null)
        {
            game.LastPlayed = best;
            game.LastPlayedSource = source;
        }
    }

    /// <summary>Bedrock: newest world folder (Store/UWP and the newer GDK version).</summary>
    public static DateTime? BedrockLastPlayed()
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftWorlds"),
        };
        string gdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Minecraft Bedrock", "Users");
        try
        {
            if (Directory.Exists(gdk))
            {
                candidates.AddRange(Directory.GetDirectories(gdk).Select(u => Path.Combine(u, "games", "com.mojang", "minecraftWorlds")));
            }
        }
        catch
        {
            // ignore
        }

        DateTime? best = null;
        foreach (string dir in candidates.Where(Directory.Exists))
        {
            try
            {
                foreach (var world in new DirectoryInfo(dir).EnumerateDirectories())
                {
                    if (best is null || world.LastWriteTime > best)
                    {
                        best = world.LastWriteTime;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return best;
    }

    private static IEnumerable<string> SafeFind(string root, string file)
    {
        try
        {
            return Directory.EnumerateFiles(root, file, SearchOption.AllDirectories).Take(1).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
