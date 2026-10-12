using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;
using Microsoft.Win32;

namespace ConstiBoost.Games;

public sealed record StorePackage(string FullName, string Name, string FamilyName, string DisplayName, string RootFolder);

/// <summary>
/// Microsoft Store and Xbox app (Game Pass / GDK) games, read from the per-user AppX repository in the registry,
/// the XboxGames folders (MicrosoftGame.config) and the Windows Game Bar game list. No WinRT, no admin rights.
/// </summary>
public static class StoreGamesScanner
{
    private const string RepositoryKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    /// <summary>Well-known Store game packages (package name prefix → display name).</summary>
    private static readonly (string prefix, string name)[] KnownStoreGames =
    {
        ("Microsoft.MinecraftUWP", "Minecraft (Bedrock Edition)"),
        ("Microsoft.MinecraftEducationEdition", "Minecraft Education"),
        ("Microsoft.MinecraftWindowsBeta", "Minecraft Preview"),
        ("ROBLOXCORPORATION.ROBLOX", "Roblox"),
        ("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection"),
        ("Microsoft.MicrosoftMahjong", "Microsoft Mahjong"),
        ("Microsoft.MicrosoftMinesweeper", "Microsoft Minesweeper"),
        ("Microsoft.MicrosoftJigsaw", "Microsoft Jigsaw"),
        ("Microsoft.MicrosoftSudoku", "Microsoft Sudoku"),
        ("king.com.", string.Empty),
        ("GAMELOFTSA.", string.Empty),
        ("Microsoft.SeaofThieves", "Sea of Thieves"),
        ("Microsoft.Ironwood", "Forza Horizon 4"),
    };

    public static List<StorePackage> ReadPackages()
    {
        var list = new List<StorePackage>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(RepositoryKey, false);
            if (root is null)
            {
                return list;
            }

            foreach (string full in root.GetSubKeyNames())
            {
                try
                {
                    using var key = root.OpenSubKey(full, false);
                    string? folder = key?.GetValue("PackageRootFolder") as string;
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    string[] parts = full.Split('_');
                    if (parts.Length < 5)
                    {
                        continue;
                    }

                    string name = parts[0];
                    string family = $"{parts[0]}_{parts[^1]}";
                    string display = ResolveIndirect(key?.GetValue("DisplayName") as string) ?? name;
                    list.Add(new StorePackage(full, name, family, display, folder));
                }
                catch
                {
                    // skip
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("AppX repository unreadable: " + ex.Message);
        }

        // Keep only the newest version per family.
        return list.GroupBy(p => p.FamilyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.FullName, StringComparer.OrdinalIgnoreCase).First())
            .ToList();
    }

    public static IEnumerable<GameInfo> Scan(List<StorePackage> packages)
    {
        var games = new List<GameInfo>();
        var seenFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Xbox app / Game Pass (GDK) folders
        foreach (string gamesRoot in XboxGameRoots())
        {
            foreach (string dir in SafeDirs(gamesRoot))
            {
                string content = Directory.Exists(Path.Combine(dir, "Content")) ? Path.Combine(dir, "Content") : dir;
                string config = Path.Combine(content, "MicrosoftGame.config");
                if (!File.Exists(config))
                {
                    continue;
                }

                try
                {
                    var doc = XDocument.Load(config);
                    string? identity = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity")?.Attribute("Name")?.Value;
                    string? display = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals")?.Attribute("DefaultDisplayName")?.Value;
                    var exeNode = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Executable");
                    string? exeName = exeNode?.Attribute("Name")?.Value;
                    string? exeId = exeNode?.Attribute("Id")?.Value;
                    var package = identity is null ? null : packages.FirstOrDefault(p => p.Name.Equals(identity, StringComparison.OrdinalIgnoreCase));
                    if (package != null)
                    {
                        seenFamilies.Add(package.FamilyName);
                    }

                    string name = CleanName(display ?? package?.DisplayName ?? Path.GetFileName(dir));
                    string? exePath = exeName is null ? null : Path.Combine(content, exeName);
                    var exes = GameLibraryService.FindExecutables(content);
                    games.Add(new GameInfo
                    {
                        Id = "xbox:" + (identity ?? Path.GetFileName(dir)),
                        Name = name,
                        Source = GameSource.Xbox,
                        InstallPath = content,
                        PackageFullName = package?.FullName,
                        AppUserModelId = package != null && exeId != null ? $"{package.FamilyName}!{exeId}" : null,
                        LaunchUri = identity != null && identity.StartsWith("Microsoft.MinecraftUWP", StringComparison.OrdinalIgnoreCase) ? "minecraft:" : null,
                        MainExecutable = exePath != null && File.Exists(exePath) ? exePath : GameLibraryService.PickMainExecutable(exes, name),
                        ExecutableNames = exes.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!,
                        ProfileKey = GameDatabase.Match(name, null, null),
                    });
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"MicrosoftGame.config in {dir} unreadable: {ex.Message}");
                }
            }
        }

        // Older Game Pass installs
        string modifiable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ModifiableWindowsApps");
        foreach (string dir in SafeDirs(modifiable))
        {
            var exes = GameLibraryService.FindExecutables(dir);
            if (exes.Count == 0)
            {
                continue;
            }

            string name = CleanName(Path.GetFileName(dir));
            games.Add(new GameInfo
            {
                Id = "xbox-legacy:" + Path.GetFileName(dir),
                Name = name,
                Source = GameSource.Xbox,
                InstallPath = dir,
                MainExecutable = GameLibraryService.PickMainExecutable(exes, name),
                ExecutableNames = exes.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!,
                ProfileKey = GameDatabase.Match(name, null, null),
            });
        }

        // 2. Known Microsoft Store games (classic UWP packages)
        foreach (var package in packages)
        {
            if (seenFamilies.Contains(package.FamilyName))
            {
                continue;
            }

            var known = KnownStoreGames.FirstOrDefault(k => package.Name.StartsWith(k.prefix, StringComparison.OrdinalIgnoreCase));
            bool isGdk = File.Exists(Path.Combine(package.RootFolder, "MicrosoftGame.config"));
            if (known.prefix is null && !isGdk)
            {
                continue;
            }

            var (appId, exe) = ReadManifest(package.RootFolder);
            string name = CleanName(string.IsNullOrEmpty(known.name) ? package.DisplayName : known.name);
            string? exePath = exe is null ? null : Path.Combine(package.RootFolder, exe);
            games.Add(new GameInfo
            {
                Id = "store:" + package.FamilyName,
                Name = name,
                Source = GameSource.Store,
                InstallPath = package.RootFolder,
                PackageFullName = package.FullName,
                AppUserModelId = appId is null ? null : $"{package.FamilyName}!{appId}",
                LaunchUri = package.Name.StartsWith("Microsoft.MinecraftUWP", StringComparison.OrdinalIgnoreCase) ? "minecraft:" : null,
                MainExecutable = exePath != null && File.Exists(exePath) ? exePath : null,
                ExecutableNames = exe is null ? new List<string>() : new List<string> { Path.GetFileNameWithoutExtension(exe) },
                ProfileKey = package.Name.StartsWith("Microsoft.Minecraft", StringComparison.OrdinalIgnoreCase) ? "minecraftbedrock" : GameDatabase.Match(name, null, null),
            });
        }

        return games;
    }

    /// <summary>Games Windows' Game Bar itself recognised (HKCU\System\GameConfigStore\Children).</summary>
    public static IEnumerable<GameInfo> ScanGameBarList()
    {
        var games = new List<GameInfo>();
        try
        {
            using var children = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children", false);
            if (children is null)
            {
                return games;
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            foreach (string sub in children.GetSubKeyNames())
            {
                try
                {
                    using var key = children.OpenSubKey(sub, false);
                    string? exe = key?.GetValue("MatchedExeFullPath") as string;
                    if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe) || exe.StartsWith(windows, StringComparison.OrdinalIgnoreCase) ||
                        exe.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string dir = Path.GetDirectoryName(exe)!;
                    // Unreal layout: <root>\<Project>\Binaries\Win64\game.exe → use <root>
                    var match = Regex.Match(dir, @"^(.*)\\[^\\]+\\Binaries\\Win(64|32)$", RegexOptions.IgnoreCase);
                    string install = match.Success ? match.Groups[1].Value : dir;
                    string name = ProductName(exe) ?? Path.GetFileName(install);
                    games.Add(new GameInfo
                    {
                        Id = "gamebar:" + exe.ToLowerInvariant(),
                        Name = CleanName(name),
                        Publisher = "Windows game list",
                        Source = GameSource.Installed,
                        InstallPath = install,
                        MainExecutable = exe,
                        ExecutableNames = new List<string> { Path.GetFileNameWithoutExtension(exe) },
                        ProfileKey = GameDatabase.Match(name, null, null),
                    });
                }
                catch
                {
                    // skip entry
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("GameConfigStore unreadable: " + ex.Message);
        }

        return games;
    }

    /// <summary>Game roots from each drive's ".GamingRoot" file (written by the Xbox app), falling back to "XboxGames".</summary>
    private static IEnumerable<string> XboxGameRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                string root = drive.RootDirectory.FullName;
                string marker = Path.Combine(root, ".GamingRoot");
                if (File.Exists(marker))
                {
                    byte[] bytes = File.ReadAllBytes(marker);
                    if (bytes.Length > 10 && bytes[0] == (byte)'R' && bytes[1] == (byte)'G' && bytes[2] == (byte)'B' && bytes[3] == (byte)'X')
                    {
                        string text = Encoding.Unicode.GetString(bytes, 8, bytes.Length - 8);
                        foreach (string part in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string candidate = Path.Combine(root, part.TrimStart('\\'));
                            if (Directory.Exists(candidate))
                            {
                                roots.Add(candidate);
                            }
                        }
                    }
                }

                string fallback = Path.Combine(root, "XboxGames");
                if (Directory.Exists(fallback))
                {
                    roots.Add(fallback);
                }
            }
            catch
            {
                // drive not readable
            }
        }

        return roots;
    }

    public static (string? appId, string? exe) ReadManifest(string packageRoot)
    {
        try
        {
            string manifest = Path.Combine(packageRoot, "AppxManifest.xml");
            if (!File.Exists(manifest))
            {
                return (null, null);
            }

            var app = XDocument.Load(manifest).Descendants().FirstOrDefault(e => e.Name.LocalName == "Application");
            return (app?.Attribute("Id")?.Value, app?.Attribute("Executable")?.Value);
        }
        catch
        {
            return (null, null);
        }
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? ProductName(string exe)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            string? name = !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName : info.FileDescription;
            return string.IsNullOrWhiteSpace(name) || name.Length > 60 ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string CleanName(string name) => Regex.Replace(name.Replace("™", string.Empty).Replace("®", string.Empty), @"\s+", " ").Trim();

    /// <summary>Resolves "@{Package?ms-resource://…}" display names via the documented SHLoadIndirectString.</summary>
    private static string? ResolveIndirect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!value.StartsWith('@'))
        {
            return value;
        }

        try
        {
            var sb = new StringBuilder(512);
            return NativeMethods.SHLoadIndirectString(value, sb, sb.Capacity, IntPtr.Zero) == 0 && sb.Length > 0 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}
