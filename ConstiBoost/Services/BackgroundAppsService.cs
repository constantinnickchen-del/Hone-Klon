using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Models;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>
/// Finds user apps that can be closed for gaming. Never lists Windows, security, driver or anti-cheat processes.
/// Known background apps (launchers, chat, music, sync) may be force-closed after a graceful attempt;
/// normal windowed apps are only asked to close so no unsaved work is lost.
/// </summary>
public static class BackgroundAppsService
{
    public static readonly HashSet<string> KnownBackgroundApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord", "spotify", "steam", "steamwebhelper", "epicgameslauncher", "epicwebhelper", "teams", "ms-teams",
        "skype", "zoom", "battle.net", "eadesktop", "origin", "riotclientservices", "riotclientux", "upc", "ubisoftconnect",
        "galaxyclient", "overwolf", "wallpaper32", "wallpaper64", "slack", "whatsapp", "telegram", "onedrive", "dropbox",
        "googledrivefs", "icloud", "adobe desktop service", "creative cloud", "ccxprocess", "obs64", "msedgewebview2",
    };

    /// <summary>Voice/stream apps keep their priority so calls and recordings do not stutter.</summary>
    public static readonly HashSet<string> KeepPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord", "teams", "ms-teams", "zoom", "skype", "obs64", "msedgewebview2",
    };

    public static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "opera_gx",
    };

    /// <summary>Processes that are never offered for closing (game, anti-cheat, ConstiBoost, shell).</summary>
    private static readonly HashSet<string> NeverOffer = new(StringComparer.OrdinalIgnoreCase)
    {
        "fortniteclient-win64-shipping", "fortnitelauncher", "easyanticheat", "easyanticheat_eos", "beservice", "battleye",
        "constiboost", "explorer", "msedgewebview2", "textinputhost", "applicationframehost", "systemsettings",
        "windowsterminal", "devenv", "taskmgr",
    };

    public static List<AppGroup> GetCandidates()
    {
        var windows = WindowEnumerator.Snapshot();
        var groups = new Dictionary<string, (List<int> pids, long ram, string? path, bool window)>(StringComparer.OrdinalIgnoreCase);
        int ownSession = Process.GetCurrentProcess().SessionId;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != ownSession || ProcessSafety.IsOwnProcess(process.Id))
                    {
                        continue;
                    }

                    string name = process.ProcessName;
                    if (NeverOffer.Contains(name))
                    {
                        continue;
                    }

                    bool known = KnownBackgroundApps.Contains(name) || BrowserProcesses.Contains(name);
                    bool hasWindow = windows.ContainsKey(process.Id);
                    if (!known && !hasWindow)
                    {
                        continue;
                    }

                    string? path = ProcessSafety.TryGetPath(process.Id);
                    if (ProcessSafety.Classify(name, path) == ProcessProtection.Critical)
                    {
                        continue;
                    }

                    long ram = 0;
                    try
                    {
                        ram = process.WorkingSet64;
                    }
                    catch
                    {
                        // ignore
                    }

                    if (groups.TryGetValue(name, out var g))
                    {
                        g.pids.Add(process.Id);
                        groups[name] = (g.pids, g.ram + ram, g.path ?? path, g.window || hasWindow);
                    }
                    else
                    {
                        groups[name] = (new List<int> { process.Id }, ram, path, hasWindow);
                    }
                }
                catch
                {
                    // exited or access denied
                }
            }
        }

        return groups.Select(kv => new AppGroup
            {
                ProcessName = kv.Key,
                DisplayName = DisplayNameFor(kv.Key, kv.Value.path),
                Path = kv.Value.path,
                ProcessIds = kv.Value.pids,
                RamBytes = kv.Value.ram,
                HasWindow = kv.Value.window,
                IsKnownBackgroundApp = KnownBackgroundApps.Contains(kv.Key),
            })
            .OrderByDescending(g => g.IsKnownBackgroundApp)
            .ThenByDescending(g => g.RamBytes)
            .ToList();
    }

    private static string DisplayNameFor(string processName, string? path)
    {
        if (path != null)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                {
                    return info.FileDescription.Trim();
                }
            }
            catch
            {
                // fall back
            }
        }

        return processName;
    }

    /// <summary>Closes all processes of a group. Returns the executable path if anything was closed (for relaunch).</summary>
    public static async Task<(bool closed, string message)> CloseGroupAsync(AppGroup group)
    {
        bool allowKill = group.IsKnownBackgroundApp;
        int closed = 0;
        string? lastError = null;
        foreach (int pid in group.ProcessIds)
        {
            var result = await ProcessService.CloseAsync(pid, group.DisplayName, allowKill);
            if (result.Success)
            {
                closed++;
            }
            else
            {
                lastError = result.Message;
            }
        }

        return closed > 0
            ? (true, L.F("{0} closed.", group.DisplayName))
            : (false, lastError ?? L.F("{0} could not be closed.", group.DisplayName));
    }
}
