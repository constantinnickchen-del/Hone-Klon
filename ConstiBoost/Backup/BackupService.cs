using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.Backup;

public sealed record RestoreReport(int Restored, int Failed, IReadOnlyList<string> Messages);

public sealed class BackupService
{
    public static BackupService Instance { get; } = new();

    public IReadOnlyList<BackupSession> GetBackups()
    {
        var list = new List<BackupSession>();
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(AppPaths.Backups))
            {
                if (!File.Exists(Path.Combine(dir, "manifest.json")))
                {
                    continue;
                }

                try
                {
                    var session = BackupSession.Load(dir);
                    if (!session.IsEmpty)
                    {
                        list.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Backup {dir} could not be read: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Backups folder could not be read: " + ex.Message);
        }

        return list.OrderByDescending(b => b.Manifest.CreatedAt).ToList();
    }

    /// <summary>Restores all entries of a backup in reverse order. HKLM values are restored through one UAC prompt.</summary>
    public async Task<RestoreReport> RestoreAsync(BackupSession session)
    {
        var messages = new List<string>();
        int restored = 0;
        int failed = 0;
        var entries = session.Manifest.Entries.AsEnumerable().Reverse().ToList();

        var adminEntries = entries.Where(e => e.RequiresAdmin).ToList();
        foreach (var entry in entries.Where(e => e.Kind == BackupKinds.ProcessorMaxState))
        {
            string[] parts = (entry.Data ?? string.Empty).Split('|');
            var result = parts.Length == 3
                ? await PowerTweakService.SetProcessorMaxStateAsync(Guid.Parse(parts[0]), uint.Parse(parts[1]), uint.Parse(parts[2]), "Restore: " + entry.Description)
                : OperationResult.Fail("Invalid backup entry");
            if (result.Success)
            {
                restored++;
            }
            else
            {
                failed++;
                messages.Add($"{entry.Description}: {result.TechnicalDetails ?? result.Message}");
            }
        }

        foreach (var entry in entries.Where(e => !e.RequiresAdmin && e.Kind != BackupKinds.ProcessorMaxState))
        {
            try
            {
                RestoreEntry(entry, session.Folder);
                restored++;
                ActionLog.Success("Restore: " + entry.Description, null, Describe(entry));
            }
            catch (Exception ex)
            {
                failed++;
                string reason = Safe.Describe(ex);
                messages.Add($"{entry.Description}: {reason}");
                ActionLog.Failed("Restore: " + entry.Description, null, Describe(entry), reason);
                AppLog.Error("Restore failed for " + entry.Summary, ex);
            }
        }

        if (adminEntries.Count > 0)
        {
            var task = new ElevatedTask
            {
                Operation = ElevatedOperations.RegistryRestore,
                Snapshots = adminEntries.Select(e => e.Registry!).ToList(),
            };
            var result = await ElevationService.RunAsync(task);
            foreach (var entry in adminEntries)
            {
                if (result.Success)
                {
                    restored++;
                    ActionLog.Success("Restore: " + entry.Description, null, Describe(entry));
                }
                else
                {
                    failed++;
                    ActionLog.Failed("Restore: " + entry.Description, null, Describe(entry), result.Message);
                }
            }

            if (!result.Success)
            {
                messages.Add(result.Message);
            }
        }

        if (failed == 0)
        {
            session.MarkRestored();
        }

        return new RestoreReport(restored, failed, messages);
    }

    public bool Delete(BackupSession session)
    {
        try
        {
            Directory.Delete(session.Folder, true);
            ActionLog.Success("Delete backup " + session.Manifest.Id, null, null);
            return true;
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Delete backup " + session.Manifest.Id, null, null, Safe.Describe(ex));
            return false;
        }
    }

    private static string Describe(BackupEntry entry) => entry.Kind switch
    {
        BackupKinds.Registry => entry.Registry?.DisplayValue ?? string.Empty,
        BackupKinds.PowerScheme or BackupKinds.PowerOverlay or BackupKinds.ProcessorMaxState => entry.Name ?? entry.Data ?? string.Empty,
        BackupKinds.File => entry.BackupFileName is null ? "(file removed)" : "original file",
        _ => entry.Data ?? string.Empty,
    };

    private static void RestoreEntry(BackupEntry entry, string folder)
    {
        switch (entry.Kind)
        {
            case BackupKinds.Registry:
                entry.Registry?.Restore();
                RegistryAfterRestore(entry);
                break;
            case BackupKinds.PowerScheme:
                PowerManager.SetActiveScheme(Guid.Parse(entry.Data!));
                break;
            case BackupKinds.PowerOverlay:
                PowerManager.SetOverlay(Guid.Parse(entry.Data!));
                break;
            case BackupKinds.SystemParameter:
                SystemParameterBackup.Restore(entry.Name!, entry.Data!);
                break;
            case BackupKinds.File:
                if (entry.BackupFileName is null)
                {
                    if (File.Exists(entry.OriginalPath))
                    {
                        File.Delete(entry.OriginalPath!);
                    }
                }
                else
                {
                    if (File.Exists(entry.OriginalPath))
                    {
                        File.SetAttributes(entry.OriginalPath!, FileAttributes.Normal);
                    }

                    File.Copy(Path.Combine(folder, entry.BackupFileName), entry.OriginalPath!, true);
                }

                break;
            default:
                throw new InvalidOperationException("Unknown backup entry type: " + entry.Kind);
        }
    }

    private static void RegistryAfterRestore(BackupEntry entry)
    {
        if (entry.Registry?.ValueName == "EnableTransparency")
        {
            WinSys.ShellNotify.BroadcastSettingChange("ImmersiveColorSet");
        }
    }
}
