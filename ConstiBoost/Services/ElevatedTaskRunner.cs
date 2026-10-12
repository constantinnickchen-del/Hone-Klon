using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

/// <summary>
/// Executes inside the elevated helper instance. Only a fixed allowlist of registry keys and
/// cleanup folders is accepted, so the helper cannot be abused to change arbitrary settings.
/// </summary>
public static class ElevatedTaskRunner
{
    private static readonly string[] AllowedHklmPrefixes =
    {
        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\",
    };

    public static int RunFromCommandLine(string taskFile, string resultFile)
    {
        ElevatedResult result;
        try
        {
            var task = JsonSerializer.Deserialize<ElevatedTask>(File.ReadAllText(taskFile))
                ?? throw new InvalidOperationException("Empty task");
            result = Execute(task);
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated helper failed", ex);
            result = new ElevatedResult { Success = false, Message = Safe.Describe(ex) };
        }

        try
        {
            File.WriteAllText(resultFile, JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated helper could not write result", ex);
            return 2;
        }

        return result.Success ? 0 : 1;
    }

    public static ElevatedResult Execute(ElevatedTask task)
    {
        try
        {
            switch (task.Operation)
            {
                case ElevatedOperations.RegistryRestore:
                    foreach (var snapshot in task.Snapshots)
                    {
                        EnsureAllowed(snapshot.Hive, snapshot.KeyPath);
                        snapshot.Restore();
                    }

                    return new ElevatedResult { Success = true, Message = $"{task.Snapshots.Count} value(s) restored." };

                case ElevatedOperations.RegistrySetDword:
                    foreach (var write in task.DwordWrites)
                    {
                        EnsureAllowed(write.Hive, write.KeyPath);
                        RegistryHelper.WriteDword(write.Hive, write.KeyPath, write.ValueName, write.Value);
                    }

                    return new ElevatedResult { Success = true, Message = $"{task.DwordWrites.Count} value(s) written." };

                case ElevatedOperations.CleanPaths:
                {
                    var allowed = CleanerService.ElevatedRoots();
                    var paths = task.Paths.Where(p => allowed.Any(a => string.Equals(Path.GetFullPath(p).TrimEnd('\\'), a.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))).ToList();
                    long freed = 0;
                    int deleted = 0;
                    int skipped = 0;
                    foreach (string path in paths)
                    {
                        var r = CleanerService.DeleteContents(path, task.MinAgeHours);
                        freed += r.FreedBytes;
                        deleted += r.DeletedFiles;
                        skipped += r.SkippedFiles;
                    }

                    return new ElevatedResult
                    {
                        Success = true,
                        Message = $"{deleted} file(s) removed, {skipped} skipped (in use or protected).",
                        FreedBytes = freed,
                        DeletedFiles = deleted,
                        SkippedFiles = skipped,
                    };
                }

                case ElevatedOperations.ProcessorMaxState:
                    PowerManager.SetProcessorMaxState(Guid.Parse(task.PowerScheme ?? string.Empty), task.AcValue, task.DcValue);
                    return new ElevatedResult { Success = true, Message = "Maximum processor state set." };

                case ElevatedOperations.FrameCapture:
                    return FrameCaptureRunner.Run(task);

                case ElevatedOperations.CreateRestorePoint:
                    return RestorePointService.CreateInProcess(task.Description ?? "ConstiBoost");

                default:
                    return new ElevatedResult { Success = false, Message = "Unknown operation: " + task.Operation };
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated task failed: " + task.Operation, ex);
            return new ElevatedResult { Success = false, Message = Safe.Describe(ex) };
        }
    }

    private static void EnsureAllowed(string hive, string keyPath)
    {
        if (string.Equals(hive, "HKCU", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(hive, "HKLM", StringComparison.OrdinalIgnoreCase) ||
            !AllowedHklmPrefixes.Any(p => keyPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException($"Registry location is not on the ConstiBoost allowlist: {hive}\\{keyPath}");
        }
    }
}
