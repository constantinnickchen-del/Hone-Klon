using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>
/// Runs a single admin-only operation through a UAC prompt. The main app keeps running as a
/// normal user; only a temporary second instance with "--elevated" is started as administrator.
/// </summary>
public static class ElevationService
{
    public const string Argument = "--elevated";

    /// <summary>
    /// Starts a long-running elevated task (frame capture) without waiting for it. Returns null when UAC was declined.
    /// If ConstiBoost already runs as administrator, the task runs on a background thread instead.
    /// </summary>
    public static Process? StartDetached(ElevatedTask task, out string? error)
    {
        error = null;
        if (SystemInfo.IsAdministrator)
        {
            var thread = new System.Threading.Thread(() => ElevatedTaskRunner.Execute(task)) { IsBackground = true, Name = "ConstiBoost elevated task" };
            thread.Start();
            return Process.GetCurrentProcess();
        }

        string id = Guid.NewGuid().ToString("N");
        string taskFile = Path.Combine(AppPaths.Temp, $"task-{id}.json");
        string resultFile = Path.Combine(AppPaths.Temp, $"task-{id}.result.json");
        try
        {
            File.WriteAllText(taskFile, JsonSerializer.Serialize(task));
            string exe = AppPaths.ExecutablePath;
            string args = $"{Argument} \"{taskFile}\" \"{resultFile}\"";
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                args = $"\"{Path.Combine(AppContext.BaseDirectory, "ConstiBoost.dll")}\" {args}";
            }

            return Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = L.T("Administrator permission was declined (UAC). Windows only allows FPS capture with administrator rights.");
            return null;
        }
        catch (Exception ex)
        {
            error = Safe.Describe(ex);
            return null;
        }
    }

    public static async Task<ElevatedResult> RunAsync(ElevatedTask task)
    {
        if (SystemInfo.IsAdministrator)
        {
            return await Task.Run(() => ElevatedTaskRunner.Execute(task));
        }

        string id = Guid.NewGuid().ToString("N");
        string taskFile = Path.Combine(AppPaths.Temp, $"task-{id}.json");
        string resultFile = Path.Combine(AppPaths.Temp, $"task-{id}.result.json");
        try
        {
            await File.WriteAllTextAsync(taskFile, JsonSerializer.Serialize(task));

            string exe = AppPaths.ExecutablePath;
            string args = $"{Argument} \"{taskFile}\" \"{resultFile}\"";
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                // Started via "dotnet ConstiBoost.dll": pass the assembly to the host.
                args = $"\"{Path.Combine(AppContext.BaseDirectory, "ConstiBoost.dll")}\" {args}";
            }

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return new ElevatedResult { Success = false, Message = L.T("The elevated helper could not be started.") };
            }

            await process.WaitForExitAsync();
            if (!File.Exists(resultFile))
            {
                return new ElevatedResult { Success = false, Message = L.F("The elevated helper exited without a result (code {0}).", process.ExitCode) };
            }

            return JsonSerializer.Deserialize<ElevatedResult>(await File.ReadAllTextAsync(resultFile))
                ?? new ElevatedResult { Success = false, Message = L.T("Invalid result from elevated helper.") };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ElevatedResult { Success = false, Message = L.T("Administrator permission was declined (UAC).") };
        }
        catch (Exception ex)
        {
            AppLog.Error("Elevated operation failed", ex);
            return new ElevatedResult { Success = false, Message = Safe.Describe(ex) };
        }
        finally
        {
            Safe.Try(() => File.Delete(taskFile));
            Safe.Try(() => File.Delete(resultFile));
        }
    }
}
