using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;
using Microsoft.Win32;

namespace ConstiBoost.Services;

/// <summary>
/// Lists and toggles startup apps with the same "StartupApproved" flags that Task Manager uses.
/// Nothing is deleted – a disabled entry can always be re-enabled here or in Task Manager.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    private static readonly string[] HighImpact =
    {
        "teams", "discord", "steam", "epicgameslauncher", "spotify", "onedrive", "creative cloud", "adobe", "battle.net",
        "eadesktop", "ea app", "origin", "riotclient", "riot client", "zoom", "skype", "dropbox", "googledrivefs", "google drive",
        "icloud", "ubisoft", "uplay", "gog galaxy", "overwolf", "wallpaper", "msedge", "chrome", "opera", "brave", "firefox",
    };

    private static readonly string[] MediumImpact =
    {
        "razer", "synapse", "lghub", "logitech", "icue", "corsair", "nvidia broadcast", "steelseries", "whatsapp", "telegram",
        "slack", "notion", "cortana", "phone link", "yourphone", "msi", "armoury", "obs",
    };

    private static readonly string[] NonEssential =
    {
        "discord", "spotify", "steam", "epicgameslauncher", "teams", "skype", "zoom", "battle.net", "eadesktop", "origin",
        "riotclient", "ubisoft", "uplay", "gog galaxy", "overwolf", "wallpaper", "slack", "whatsapp", "telegram", "msedge",
        "chrome", "opera", "brave", "cortana",
    };

    private static readonly string[] Protected =
    {
        "securityhealth", "windows defender", "windows security", "rtkaud", "realtek", "etdctrl", "elan", "syntp", "synaptics",
        "amdnoisesuppression", "radeonsoftware", "amd software", "igfx", "intel graphics", "nvidia container", "nvcontainer",
        "ctfmon", "bluetooth", "touchpad", "precisiontouch", "hotkey", "acer quick access", "quickaccess", "accessibility",
        "narrator", "audio", "lenovo vantage", "myasus", "dell", "hp ", "waves", "maxxaudio", "dolby",
    };

    public static List<StartupEntry> GetEntries()
    {
        var list = new List<StartupEntry>();
        ReadRunKey(list, "HKCU", RunKey, "Registry (current user)", ApprovedBase + "Run", false);
        ReadRunKey(list, "HKLM", RunKey, "Registry (all users)", ApprovedBase + "Run", true);
        ReadRunKey(list, "HKLM", RunKey32, "Registry (all users, 32-bit)", ApprovedBase + "Run32", true);
        ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder (current user)", "HKCU", false);
        ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)", "HKLM", true);
        return list.OrderBy(e => e.IsProtected).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ReadRunKey(List<StartupEntry> list, string hive, string keyPath, string location, string approvedKey, bool admin)
    {
        try
        {
            using var root = RegistryHelper.OpenBase(hive);
            using var key = root.OpenSubKey(keyPath, false);
            if (key is null)
            {
                return;
            }

            foreach (string name in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name) || key.GetValue(name) is not string command)
                {
                    continue;
                }

                string? target = ExtractExecutable(command);
                list.Add(Create(name, command, target, location, hive, approvedKey, name, admin));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Startup key {hive}\\{keyPath} unreadable: {ex.Message}");
        }
    }

    private static void ReadFolder(List<StartupEntry> list, string folder, string location, string hive, bool admin)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(folder))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? target = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ResolveShortcut(file) : file;
                list.Add(Create(Path.GetFileNameWithoutExtension(file), file, target, location, hive, ApprovedBase + "StartupFolder", fileName, admin));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Startup folder {folder} unreadable: {ex.Message}");
        }
    }

    private static StartupEntry Create(string name, string command, string? target, string location, string hive, string approvedKey, string approvedValue, bool admin)
    {
        string publisher = "Unknown publisher";
        if (target != null && File.Exists(target))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(target);
                if (!string.IsNullOrWhiteSpace(info.CompanyName))
                {
                    publisher = info.CompanyName.Trim();
                }
            }
            catch
            {
                // keep unknown
            }
        }

        string haystack = (name + " " + command + " " + publisher).ToLowerInvariant();
        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        bool inWindows = target != null && target.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
        bool isMicrosoftSystem = publisher.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) && inWindows;
        bool isProtected = inWindows || isMicrosoftSystem || Protected.Any(p => haystack.Contains(p));
        string protectionReason = isProtected
            ? (inWindows ? "Windows system component" : "System, driver or hardware utility – kept enabled for safety")
            : string.Empty;

        string impact = HighImpact.Any(h => haystack.Contains(h)) ? "High (typical)"
            : MediumImpact.Any(m => haystack.Contains(m)) ? "Medium (typical)"
            : "Not measured";

        return new StartupEntry
        {
            Name = name,
            Command = command,
            TargetPath = target,
            Publisher = publisher,
            Location = location,
            Impact = impact,
            IsProtected = isProtected,
            ProtectionReason = protectionReason,
            RequiresAdmin = admin,
            IsKnownNonEssential = !isProtected && NonEssential.Any(n => haystack.Contains(n)),
            ApprovedHive = hive,
            ApprovedKeyPath = approvedKey,
            ApprovedValueName = approvedValue,
            IsEnabled = ReadApproved(hive, approvedKey, approvedValue),
        };
    }

    /// <summary>StartupApproved: first byte even (0x02/0x06) = enabled, odd (0x03/0x07) = disabled, missing = enabled.</summary>
    private static bool ReadApproved(string hive, string keyPath, string valueName)
    {
        try
        {
            using var root = RegistryHelper.OpenBase(hive);
            using var key = root.OpenSubKey(keyPath, false);
            if (key?.GetValue(valueName) is byte[] data && data.Length > 0)
            {
                return (data[0] & 1) == 0;
            }
        }
        catch
        {
            // fall through
        }

        return true;
    }

    private static byte[] BuildApproved(bool enabled)
    {
        var data = new byte[12];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
        }

        return data;
    }

    public static async Task<OperationResult> SetEnabledAsync(StartupEntry entry, bool enabled, BackupSession backup)
    {
        if (entry.IsProtected)
        {
            return OperationResult.Fail($"{entry.Name} is protected: {entry.ProtectionReason}");
        }

        string oldText = entry.IsEnabled ? "Enabled" : "Disabled";
        string newText = enabled ? "Enabled" : "Disabled";
        string action = $"Startup app: {entry.Name}";
        try
        {
            backup.CaptureRegistry(entry.ApprovedHive, entry.ApprovedKeyPath, entry.ApprovedValueName, $"Startup state of {entry.Name}");
            byte[] value = BuildApproved(enabled);
            if (entry.ApprovedHive == "HKCU")
            {
                using var root = RegistryHelper.OpenBase("HKCU");
                using var key = root.CreateSubKey(entry.ApprovedKeyPath, true);
                key.SetValue(entry.ApprovedValueName, value, RegistryValueKind.Binary);
            }
            else
            {
                var snapshot = new RegistryValueSnapshot
                {
                    Hive = "HKLM",
                    KeyPath = entry.ApprovedKeyPath,
                    ValueName = entry.ApprovedValueName,
                    Existed = true,
                    Kind = nameof(RegistryValueKind.Binary),
                    Data = Convert.ToBase64String(value),
                };
                var result = await ElevationService.RunAsync(new ElevatedTask
                {
                    Operation = ElevatedOperations.RegistryRestore,
                    Snapshots = { snapshot },
                });
                if (!result.Success)
                {
                    ActionLog.Failed(action, oldText, newText, result.Message);
                    return OperationResult.Fail("This change couldn't be applied: " + result.Message);
                }
            }

            entry.IsEnabled = ReadApproved(entry.ApprovedHive, entry.ApprovedKeyPath, entry.ApprovedValueName);
            if (entry.IsEnabled != enabled)
            {
                ActionLog.Failed(action, oldText, newText, "Value did not change after writing");
                return OperationResult.Fail("Windows did not accept the change.");
            }

            ActionLog.Success(action, oldText, newText);
            return OperationResult.Ok($"{entry.Name}: {newText}");
        }
        catch (Exception ex)
        {
            ActionLog.Failed(action, oldText, newText, Safe.Describe(ex));
            AppLog.Error("Startup toggle failed", ex);
            return OperationResult.Fail("This change couldn't be applied: " + Safe.Describe(ex));
        }
    }

    public static string? ExtractExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (expanded.StartsWith('"'))
        {
            int end = expanded.IndexOf('"', 1);
            return end > 1 ? expanded[1..end] : null;
        }

        var match = Regex.Match(expanded, @"^(.+?\.exe)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : expanded.Split(' ')[0];
    }

    private static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath;
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }
}
