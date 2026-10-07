using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed record ProcessSample(int Pid, string Name, string? Path, double Cpu, long Ram, double? Gpu, string Status, ProcessProtection Protection);

/// <summary>Process list with CPU/RAM/GPU usage. Only runs while the Processes tab is visible.</summary>
public sealed class ProcessSampler
{
    private readonly Dictionary<int, (TimeSpan cpu, DateTime at)> _previous = new();
    private readonly Dictionary<int, string?> _paths = new();
    private readonly GpuUsageSampler _gpu = new();

    public List<ProcessSample> Sample()
    {
        _gpu.Sample();
        var windows = WindowEnumerator.Snapshot();
        var now = DateTime.UtcNow;
        var seen = new HashSet<int>();
        var list = new List<ProcessSample>();
        int cores = Environment.ProcessorCount;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    int pid = process.Id;
                    if (pid == 0)
                    {
                        continue;
                    }

                    seen.Add(pid);
                    string name = process.ProcessName;
                    double cpu = 0;
                    try
                    {
                        var total = process.TotalProcessorTime;
                        if (_previous.TryGetValue(pid, out var prev))
                        {
                            double elapsed = (now - prev.at).TotalMilliseconds;
                            if (elapsed > 0)
                            {
                                cpu = Math.Clamp((total - prev.cpu).TotalMilliseconds / elapsed / cores * 100, 0, 100);
                            }
                        }

                        _previous[pid] = (total, now);
                    }
                    catch
                    {
                        // Protected process: CPU time not readable without admin.
                    }

                    long ram = 0;
                    try
                    {
                        ram = process.WorkingSet64;
                    }
                    catch
                    {
                        // not readable
                    }

                    if (!_paths.TryGetValue(pid, out string? path))
                    {
                        path = ProcessSafety.TryGetPath(pid);
                        _paths[pid] = path;
                    }

                    int session = Safe.Try(() => process.SessionId, -1);
                    string status = session == 0 ? "Service"
                        : windows.TryGetValue(pid, out var w) ? (w.IsHung ? "Not responding" : "Running")
                        : "Background";
                    double? gpu = _gpu.IsAvailable ? (_gpu.PerProcess.TryGetValue(pid, out double g) ? g : 0) : null;
                    var protection = session == 0 ? ProcessProtection.Critical : ProcessSafety.Classify(name, path);
                    list.Add(new ProcessSample(pid, name, path, cpu, ram, gpu, status, protection));
                }
                catch
                {
                    // Process exited during enumeration.
                }
            }
        }

        foreach (int dead in _previous.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _previous.Remove(dead);
            _paths.Remove(dead);
        }

        return list;
    }
}

public static class ProcessService
{
    /// <summary>Asks a process to close (like clicking X). Kill is only used when allowed and the graceful close failed.</summary>
    public static async Task<OperationResult> CloseAsync(int pid, string name, bool allowKill)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch
        {
            return OperationResult.Ok($"{name} is no longer running.");
        }

        using (process)
        {
            if (!ProcessSafety.CanTouch(process, out string reason))
            {
                ActionLog.Failed($"Close process {name}", "Running", "Closed", reason);
                return OperationResult.Fail($"{name} can't be closed: {reason}");
            }

            try
            {
                bool requested = false;
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    requested = process.CloseMainWindow();
                }

                if (requested)
                {
                    using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(4));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // still running
                    }
                }

                if (!process.HasExited)
                {
                    if (!allowKill)
                    {
                        ActionLog.Failed($"Close process {name}", "Running", "Closed", "The app did not close (it may be asking to save).");
                        return OperationResult.Fail($"{name} did not close – it may be waiting for you to save something.");
                    }

                    process.Kill(entireProcessTree: false);
                    await process.WaitForExitAsync();
                }

                ActionLog.Success($"Close process {name} (PID {pid})", "Running", "Closed");
                return OperationResult.Ok($"{name} closed.");
            }
            catch (Exception ex)
            {
                ActionLog.Failed($"Close process {name}", "Running", "Closed", Safe.Describe(ex));
                return OperationResult.Fail($"{name} couldn't be closed: {Safe.Describe(ex)}");
            }
        }
    }
}
