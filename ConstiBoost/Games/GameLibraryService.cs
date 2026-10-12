using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Games;

/// <summary>
/// Finds installed games from real sources only: Steam library manifests, Epic Games Launcher manifests and
/// the Windows uninstall registry (GOG, EA, Ubisoft, Riot, Battle.net, Rockstar, Roblox, Minecraft, …).
/// </summary>
public static class GameLibraryService
{
    private static readonly string[] GamePublishers =
    {
        "Riot Games", "Electronic Arts", "EA Games", "Ubisoft", "Blizzard", "Activision", "Mojang", "Roblox", "GOG.com", "GOG Limited",
        "Rockstar Games", "Bethesda", "CD PROJEKT", "miHoYo", "HoYoverse", "COGNOSPHERE", "Wargaming", "Epic Games, Inc.", "Bandai Namco",
        "SEGA", "Square Enix", "Capcom", "2K", "Paradox", "Krafton", "Respawn", "Hi-Rez", "Gaijin", "NetEase", "Innersloth", "Mediatonic",
    };

    private static readonly string[] GamePathHints =
    {
        @"\GOG Galaxy\Games\", @"\Riot Games\", @"\EA Games\", @"\Origin Games\", @"\Ubisoft Game Launcher\games\", @"\Rockstar Games\",
        @"\Battle.net\", @"\Blizzard\", @"\Games\", @"\Roblox\", @"\Minecraft",
    };

    private static readonly Regex NotAGame = new(
        @"Launcher|Redistributable|Runtime|Steamworks|Galaxy Client|EA app|Origin$|Ubisoft Connect|Battle\.net$|Riot Client$|Anti-?Cheat|Driver|SDK|Overlay|Uninstall|Updater|Helper|Service|Prerequisite|DirectX|Visual C\+\+|\.NET|Proton|Steam Linux|SteamVR",
        RegexOptions.IgnoreCase);

    private static readonly HashSet<string> SkipSteamApps = new() { "228980", "250820", "1070560", "1391110", "1628350", "1493710", "2180100" };

    public static List<GameInfo> Scan()
    {
        var games = new List<GameInfo>();
        Try(() => games.AddRange(ScanSteam()), "Steam");
        Try(() => games.AddRange(ScanEpic()), "Epic");
        var packages = new List<StorePackage>();
        Try(() => packages = StoreGamesScanner.ReadPackages(), "AppX repository");
        Try(() => games.AddRange(StoreGamesScanner.Scan(packages)), "Microsoft Store / Xbox");
        Try(() =>
        {
            if (MinecraftScanner.ScanJava(packages) is { } java)
            {
                games.Add(java);
            }
        }, "Minecraft Java");
        Try(() => games.AddRange(ScanUninstallRegistry(games)), "Uninstall registry");
        Try(() => games.AddRange(BrowserGamesScanner.Scan()), "Browsers");
        Try(() =>
        {
            // Games Windows' Game Bar knows about that no launcher reported (skip anything inside known games).
            var roots = games.SelectMany(x => x.ProcessRoots).Select(r => NormalizePath(r) + "\\").ToList();
            games.AddRange(StoreGamesScanner.ScanGameBarList().Where(gb =>
                gb.MainExecutable != null && !roots.Any(r => gb.MainExecutable.StartsWith(r, StringComparison.OrdinalIgnoreCase))));
        }, "Game Bar list");

        // Deduplicate by install path
        return games
            .GroupBy(g => NormalizePath(g.InstallPath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Source).First())
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Try(Action action, string source)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Game scan ({source}) failed: {ex.Message}");
        }
    }

    public static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch
        {
            return path.TrimEnd('\\', '/');
        }
    }

    // ---------------- Steam ----------------
    public static string? SteamPath()
    {
        string? path = RegistryHelper.ReadString("HKCU", @"Software\Valve\Steam", "SteamPath")
                       ?? RegistryHelper.ReadString("HKLM", @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        return path?.Replace('/', '\\');
    }

    private static IEnumerable<GameInfo> ScanSteam()
    {
        string? steam = SteamPath();
        if (steam is null || !Directory.Exists(steam))
        {
            yield break;
        }

        var libraries = new List<string> { steam };
        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            var root = VdfNode.Parse(File.ReadAllText(vdf));
            var folders = root.Child("libraryfolders") ?? root.Child("LibraryFolders");
            if (folders != null)
            {
                foreach (var child in folders.Children.Values)
                {
                    if (child.Get("path") is { } p)
                    {
                        libraries.Add(p.Replace(@"\\", @"\"));
                    }
                }

                // Old format: "1" "D:\\SteamLibrary"
                foreach (var (key, value) in folders.Values)
                {
                    if (int.TryParse(key, out _))
                    {
                        libraries.Add(value.Replace(@"\\", @"\"));
                    }
                }
            }
        }

        foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps))
            {
                continue;
            }

            foreach (string acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                GameInfo? game = null;
                try
                {
                    var state = VdfNode.Parse(File.ReadAllText(acf)).Child("AppState");
                    if (state is null)
                    {
                        continue;
                    }

                    string? appId = state.Get("appid");
                    string? name = state.Get("name");
                    string? dir = state.Get("installdir");
                    if (appId is null || name is null || dir is null || SkipSteamApps.Contains(appId) || NotAGame.IsMatch(name))
                    {
                        continue;
                    }

                    string install = Path.Combine(apps, "common", dir);
                    if (!Directory.Exists(install))
                    {
                        continue;
                    }

                    var exes = FindExecutables(install);
                    game = new GameInfo
                    {
                        Id = "steam:" + appId,
                        Name = name,
                        Source = GameSource.Steam,
                        InstallPath = install,
                        SteamAppId = appId,
                        MainExecutable = PickMainExecutable(exes, name),
                        ExecutableNames = exes.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!,
                        ProfileKey = GameDatabase.Match(name, appId, null),
                        SizeBytes = long.TryParse(state.Get("SizeOnDisk"), out long size) && size > 0 ? size : null,
                    };
                    if (long.TryParse(state.Get("LastPlayed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix) && unix > 0)
                    {
                        game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                        game.LastPlayedSource = "Steam";
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Steam manifest {acf} unreadable: {ex.Message}");
                }

                if (game != null)
                {
                    yield return game;
                }
            }
        }
    }

    // ---------------- Epic ----------------
    private static IEnumerable<GameInfo> ScanEpic()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.item"))
        {
            GameInfo? game = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var r = doc.RootElement;
                string? Str(string key) => r.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                bool Bool(string key) => r.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.True;
                if (Bool("bIsIncompleteInstall") || (r.TryGetProperty("bIsApplication", out var isApp) && isApp.ValueKind == JsonValueKind.False))
                {
                    continue;
                }

                string? name = Str("DisplayName");
                string? install = Str("InstallLocation");
                string? appName = Str("AppName");
                if (name is null || install is null || !Directory.Exists(install) || NotAGame.IsMatch(name))
                {
                    continue;
                }

                string? launch = Str("LaunchExecutable");
                string? exe = launch is null ? null : Path.Combine(install, launch.Replace('/', '\\'));
                var exes = FindExecutables(install);
                string? ns = Str("CatalogNamespace");
                string? item = Str("CatalogItemId");
                game = new GameInfo
                {
                    Id = "epic:" + (appName ?? name),
                    Name = name,
                    Source = GameSource.Epic,
                    InstallPath = install,
                    EpicAppName = appName,
                    EpicLaunchId = ns != null && item != null ? $"{ns}:{item}:{appName}" : appName,
                    EpicLaunchExecutable = launch,
                    MainExecutable = exe != null && File.Exists(exe) ? exe : PickMainExecutable(exes, name),
                    ExecutableNames = exes.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!,
                    ProfileKey = GameDatabase.Match(name, null, appName),
                    SizeBytes = r.TryGetProperty("InstallSize", out var s) && s.TryGetInt64(out long size) && size > 0 ? size : null,
                };
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Epic manifest {file} unreadable: {ex.Message}");
            }

            if (game != null)
            {
                yield return game;
            }
        }
    }

    // ---------------- Windows uninstall registry ----------------
    private static IEnumerable<GameInfo> ScanUninstallRegistry(List<GameInfo> known)
    {
        var knownPaths = new HashSet<string>(known.Select(k => NormalizePath(k.InstallPath)), StringComparer.OrdinalIgnoreCase);
        var roots = new (string hive, string path)[]
        {
            ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        };
        var results = new List<GameInfo>();
        foreach (var (hive, path) in roots)
        {
            try
            {
                using var root = RegistryHelper.OpenBase(hive);
                using var key = root.OpenSubKey(path, false);
                if (key is null)
                {
                    continue;
                }

                foreach (string sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var entry = key.OpenSubKey(sub, false);
                        if (entry is null || entry.GetValue("SystemComponent") is int sc && sc == 1)
                        {
                            continue;
                        }

                        string? name = entry.GetValue("DisplayName") as string;
                        string? publisher = entry.GetValue("Publisher") as string;
                        string? install = entry.GetValue("InstallLocation") as string;
                        string? uninstall = entry.GetValue("UninstallString") as string;
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(install) || sub.StartsWith("Steam App", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        install = install.Trim('"');
                        bool publisherMatch = publisher != null && GamePublishers.Any(p => publisher.Contains(p, StringComparison.OrdinalIgnoreCase));
                        bool pathMatch = GamePathHints.Any(h => install.Contains(h, StringComparison.OrdinalIgnoreCase));
                        if ((!publisherMatch && !pathMatch) || NotAGame.IsMatch(name) || !Directory.Exists(install))
                        {
                            continue;
                        }

                        string norm = NormalizePath(install);
                        if (!knownPaths.Add(norm))
                        {
                            continue;
                        }

                        var exes = FindExecutables(install);
                        if (exes.Count == 0)
                        {
                            continue;
                        }

                        long? sizeKb = entry.GetValue("EstimatedSize") is int kb && kb > 0 ? kb : null;
                        results.Add(new GameInfo
                        {
                            Id = "reg:" + sub,
                            Name = name.Trim(),
                            Publisher = publisher,
                            Source = GameSource.Installed,
                            InstallPath = install,
                            UninstallString = uninstall,
                            MainExecutable = PickMainExecutable(exes, name),
                            ExecutableNames = exes.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!,
                            ProfileKey = GameDatabase.Match(name, null, null),
                            SizeBytes = sizeKb * 1024,
                        });
                    }
                    catch
                    {
                        // skip broken entry
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Uninstall key {hive}\\{path} unreadable: {ex.Message}");
            }
        }

        return results;
    }

    private static readonly Regex HelperExe = new(@"unins|crash|report|redist|setup|install|vc_|dxsetup|launcher|helper|update|easyanticheat|battleye|ue4prereq|dotnet|cefprocess|webhelper|overlay|service", RegexOptions.IgnoreCase);

    /// <summary>All .exe files up to 4 levels deep (used to recognise the running game).</summary>
    public static List<string> FindExecutables(string root)
    {
        var list = new List<string>();
        try
        {
            var stack = new Stack<(string dir, int depth)>();
            stack.Push((root, 0));
            while (stack.Count > 0 && list.Count < 200)
            {
                var (dir, depth) = stack.Pop();
                try
                {
                    list.AddRange(Directory.EnumerateFiles(dir, "*.exe"));
                    if (depth < 4)
                    {
                        foreach (string sub in Directory.EnumerateDirectories(dir))
                        {
                            var info = new DirectoryInfo(sub);
                            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                stack.Push((sub, depth + 1));
                            }
                        }
                    }
                }
                catch
                {
                    // access denied
                }
            }
        }
        catch
        {
            // ignore
        }

        return list;
    }

    /// <summary>The main game exe: shipping build / name match / largest non-helper exe.</summary>
    public static string? PickMainExecutable(List<string> exes, string gameName)
    {
        var candidates = exes.Where(e => !HelperExe.IsMatch(Path.GetFileName(e))).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var shipping = candidates.FirstOrDefault(e => e.Contains("Shipping", StringComparison.OrdinalIgnoreCase) || e.Contains("-Win64-", StringComparison.OrdinalIgnoreCase));
        if (shipping != null)
        {
            return shipping;
        }

        string compact = Regex.Replace(gameName, @"[^A-Za-z0-9]", string.Empty);
        var byName = candidates.FirstOrDefault(e => compact.Length > 2 && Regex.Replace(Path.GetFileNameWithoutExtension(e), @"[^A-Za-z0-9]", string.Empty)
            .Contains(compact.Length > 8 ? compact[..8] : compact, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
        {
            return byName;
        }

        return candidates.OrderByDescending(e =>
        {
            try
            {
                return new FileInfo(e).Length;
            }
            catch
            {
                return 0L;
            }
        }).First();
    }

    public static long FolderSize(string root)
    {
        long total = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFileSystemInfos())
                {
                    if ((f.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    if (f is FileInfo fi)
                    {
                        total += fi.Length;
                    }
                    else
                    {
                        stack.Push(f.FullName);
                    }
                }
            }
            catch
            {
                // skip
            }
        }

        return total;
    }
}
