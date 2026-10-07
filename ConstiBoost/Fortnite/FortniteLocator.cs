using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConstiBoost.Logging;

namespace ConstiBoost.Fortnite;

public sealed record FortniteInstall(string InstallPath, string ExecutablePath, string? Version, string Source)
{
    /// <summary>Epic launcher app id "namespace:itemId:appName" from the manifest, if known.</summary>
    public string? LauncherAppId { get; init; }
}

/// <summary>
/// Finds Fortnite through the Epic Games Launcher's own install manifests, then falls back to common
/// install folders. An install only counts if the real game executable exists.
/// </summary>
public static class FortniteLocator
{
    public const string ProcessName = "FortniteClient-Win64-Shipping";
    private const string RelativeExe = @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe";

    public static FortniteInstall? Locate()
    {
        return FromManifests() ?? FromLauncherInstalled() ?? FromCommonFolders();
    }

    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static FortniteInstall? FromManifests()
    {
        string dir = Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        try
        {
            if (!Directory.Exists(dir))
            {
                return null;
            }

            foreach (string file in Directory.EnumerateFiles(dir, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    var root = doc.RootElement;
                    string? appName = root.TryGetProperty("AppName", out var a) ? a.GetString() : null;
                    string? display = root.TryGetProperty("DisplayName", out var d) ? d.GetString() : null;
                    if (!string.Equals(appName, "Fortnite", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(display, "Fortnite", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? location = root.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                    string? version = root.TryGetProperty("AppVersionString", out var v) ? v.GetString() : null;
                    var install = Validate(location, version, "Epic Games Launcher manifest");
                    if (install != null)
                    {
                        string? ns = root.TryGetProperty("CatalogNamespace", out var n) ? n.GetString() : null;
                        string? item = root.TryGetProperty("CatalogItemId", out var c) ? c.GetString() : null;
                        return string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(item)
                            ? install
                            : install with { LauncherAppId = $"{ns}:{item}:{appName ?? "Fortnite"}" };
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Epic manifest {file} unreadable: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Epic manifests unreadable: " + ex.Message);
        }

        return null;
    }

    private static FortniteInstall? FromLauncherInstalled()
    {
        string file = Path.Combine(ProgramData, "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.TryGetProperty("InstallationList", out var list))
            {
                return null;
            }

            foreach (var item in list.EnumerateArray())
            {
                string? appName = item.TryGetProperty("AppName", out var a) ? a.GetString() : null;
                if (!string.Equals(appName, "Fortnite", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? location = item.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                string? version = item.TryGetProperty("AppVersion", out var v) ? v.GetString() : null;
                var install = Validate(location, version, "Epic LauncherInstalled.dat");
                if (install != null)
                {
                    return install;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LauncherInstalled.dat unreadable: " + ex.Message);
        }

        return null;
    }

    private static FortniteInstall? FromCommonFolders()
    {
        var candidates = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                string root = drive.RootDirectory.FullName;
                candidates.Add(Path.Combine(root, "Program Files", "Epic Games", "Fortnite"));
                candidates.Add(Path.Combine(root, "Program Files (x86)", "Epic Games", "Fortnite"));
                candidates.Add(Path.Combine(root, "Epic Games", "Fortnite"));
                candidates.Add(Path.Combine(root, "Games", "Epic Games", "Fortnite"));
                candidates.Add(Path.Combine(root, "Games", "Fortnite"));
                candidates.Add(Path.Combine(root, "Fortnite"));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Drive enumeration failed: " + ex.Message);
        }

        return candidates.Select(c => Validate(c, null, "Common install folder")).FirstOrDefault(i => i != null);
    }

    private static FortniteInstall? Validate(string? location, string? version, string source)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        try
        {
            string exe = Path.Combine(location, RelativeExe);
            return File.Exists(exe) ? new FortniteInstall(location, exe, version, source) : null;
        }
        catch
        {
            return null;
        }
    }
}
