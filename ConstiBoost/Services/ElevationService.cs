using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

/// <summary>
/// Runs a single admin-only operation through a UAC prompt. The main app keeps running as a
/// normal user; only a temporary second instance with "--elevated" is started as administrator.
/// </summary>
public static class ElevationService
{
    public const string Argument = "--elevated";

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
                return new ElevatedResult { Success = false, Message = "The elevated helper could not be started." };
            }

            await process.WaitForExitAsync();
            if (!File.Exists(resultFile))
            {
                return new ElevatedResult { Success = false, Message = $"The elevated helper exited without a result (code {process.ExitCode})." };
            }

            return JsonSerializer.Deserialize<ElevatedResult>(await File.ReadAllTextAsync(resultFile))
                ?? new ElevatedResult { Success = false, Message = "Invalid result from elevated helper." };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ElevatedResult { Success = false, Message = "Administrator permission was declined (UAC)." };
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
