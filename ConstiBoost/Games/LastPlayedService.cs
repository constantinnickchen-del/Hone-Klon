using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using Microsoft.Win32;

namespace ConstiBoost.Games;

/// <summary>
/// Determines when a game was last played from real local data only:
/// Steam's own "LastPlayed", Windows UserAssist (programs started via Explorer/Start menu),
/// Unreal Engine game logs, and sessions ConstiBoost itself observed. Unknown stays unknown.
/// </summary>
public static class LastPlayedService
{
    private const string UserAssistKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    private static readonly Dictionary<string, string> KnownFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{6D809377-6AF0-444B-8957-A3773F02200E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        ["{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ["{F38BF404-1D43-42F2-9305-67DE0B28FC23}"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        ["{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}"] = Environment.GetFolderPath(Environment.SpecialFolder.System),
    };

    private static string SessionsFile => Path.Combine(AppPaths.Root, "game-sessions.json");

    public static void Apply(IEnumerable<GameInfo> games)
    {
        var userAssist = Safe.Try(ReadUserAssist, new List<(string path, DateTime time)>(), "UserAssist");
        var sessions = LoadSessions();
        foreach (var game in games)
        {
            string root = GameLibraryService.NormalizePath(game.InstallPath) + "\\";
            Consider(game, userAssist.Where(u => u.path.StartsWith(root, StringComparison.OrdinalIgnoreCase)).Select(u => (DateTime?)u.time).DefaultIfEmpty(null).Max(), "Windows");
            if (sessions.TryGetValue(game.Id, out var seen))
            {
                Consider(game, seen, "ConstiBoost");
            }

            Consider(game, UnrealLogTime(game), "game log");
            if (game.PackageFullName != null)
            {
                Consider(game, PackageDataTime(game.PackageFullName), "app data");
            }

            if (game.ProfileKey == "minecraftbedrock" || (game.Source is GameSource.Store or GameSource.Xbox && game.Name.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) && !game.Name.Contains("Launcher", StringComparison.OrdinalIgnoreCase)))
            {
                Consider(game, MinecraftScanner.BedrockLastPlayed(), "Minecraft worlds");
            }
        }
    }

    private static void Consider(GameInfo game, DateTime? time, string source)
    {
        if (time is DateTime t && t <= DateTime.Now.AddMinutes(5) && (game.LastPlayed is null || t > game.LastPlayed))
        {
            game.LastPlayed = t;
            game.LastPlayedSource = source;
        }
    }

    /// <summary>Store apps update %LOCALAPPDATA%\Packages\&lt;family&gt;\Settings\settings.dat when they run.</summary>
    private static DateTime? PackageDataTime(string fullName)
    {
        try
        {
            string[] parts = fullName.Split('_');
            if (parts.Length < 5)
            {
                return null;
            }

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", $"{parts[0]}_{parts[^1]}");
            string settings = Path.Combine(dir, "Settings", "settings.dat");
            return File.Exists(settings) ? File.GetLastWriteTime(settings) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Unreal Engine games write logs to %LOCALAPPDATA%\&lt;Project&gt;\Saved\Logs while running.</summary>
    private static DateTime? UnrealLogTime(GameInfo game)
    {
        try
        {
            string? project = game.EpicLaunchExecutable?.Replace('\\', '/').Split('/').FirstOrDefault();
            if (string.IsNullOrEmpty(project) && game.MainExecutable is { } exe)
            {
                // <install>\<Project>\Binaries\Win64\<exe>
                var parts = exe.Split('\\');
                int bin = Array.FindIndex(parts, p => p.Equals("Binaries", StringComparison.OrdinalIgnoreCase));
                project = bin > 0 ? parts[bin - 1] : null;
            }

            if (string.IsNullOrEmpty(project))
            {
                return null;
            }

            string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), project, "Saved", "Logs");
            if (!Directory.Exists(logs))
            {
                return null;
            }

            var newest = new DirectoryInfo(logs).EnumerateFiles("*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            return newest?.LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    private static List<(string path, DateTime time)> ReadUserAssist()
    {
        var result = new List<(string, DateTime)>();
        using var root = Registry.CurrentUser.OpenSubKey(UserAssistKey, false);
        if (root is null)
        {
            return result;
        }

        foreach (string guid in root.GetSubKeyNames())
        {
            using var count = root.OpenSubKey(guid + @"\Count", false);
            if (count is null)
            {
                continue;
            }

            foreach (string encoded in count.GetValueNames())
            {
                if (count.GetValue(encoded) is not byte[] data || data.Length < 68)
                {
                    continue;
                }

                long fileTime = BitConverter.ToInt64(data, 60);
                if (fileTime <= 0)
                {
                    continue;
                }

                string path = Rot13(encoded);
                foreach (var (folderGuid, folder) in KnownFolders)
                {
                    if (path.StartsWith(folderGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        path = folder + path[folderGuid.Length..];
                        break;
                    }
                }

                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    result.Add((path, DateTime.FromFileTimeUtc(fileTime).ToLocalTime()));
                }
                catch
                {
                    // invalid time
                }
            }
        }

        return result;
    }

    private static string Rot13(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            sb.Append(c switch
            {
                >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
                >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
                _ => c,
            });
        }

        return sb.ToString();
    }

    // ---------- sessions observed by ConstiBoost ----------
    public static Dictionary<string, DateTime> LoadSessions()
    {
        try
        {
            if (File.Exists(SessionsFile))
            {
                return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(SessionsFile)) ?? new();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Game sessions unreadable: " + ex.Message);
        }

        return new();
    }

    public static void RecordSession(string gameId)
    {
        try
        {
            var sessions = LoadSessions();
            sessions[gameId] = DateTime.Now;
            File.WriteAllText(SessionsFile, JsonSerializer.Serialize(sessions));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Game session could not be saved: " + ex.Message);
        }
    }
}
