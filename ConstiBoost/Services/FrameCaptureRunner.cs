using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>One second of frame data for one process (written as one JSON line by the capture helper).</summary>
public sealed class FrameSecond
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Frames { get; set; }
    public List<float> FrameTimesMs { get; set; } = new();
}

public sealed class FrameLine
{
    public double T { get; set; }
    public List<FrameSecond> P { get; set; } = new();
}

/// <summary>
/// Runs inside the elevated helper: records DirectX presents via ETW and appends one JSON line per second
/// to the output file until the stop file appears, the duration ends or ConstiBoost exits.
/// </summary>
public static class FrameCaptureRunner
{
    private static readonly HashSet<string> Ignore = new(StringComparer.OrdinalIgnoreCase) { "dwm", "ConstiBoost", "explorer", "TextInputHost", "SearchHost", "ShellExperienceHost" };

    public static ElevatedResult Run(ElevatedTask task)
    {
        if (string.IsNullOrEmpty(task.OutputFile) || string.IsNullOrEmpty(task.StopFile))
        {
            return new ElevatedResult { Success = false, Message = L.T("Invalid capture task.") };
        }

        var names = new Dictionary<int, string>();
        var lastQpc = new Dictionary<int, long>();
        var deadline = task.DurationSeconds > 0 ? DateTime.UtcNow.AddSeconds(task.DurationSeconds + 5) : DateTime.UtcNow.AddHours(4);
        var started = Stopwatch.StartNew();
        try
        {
            using var capture = new EtwPresentCapture();
            capture.Start();
            using var writer = new StreamWriter(new FileStream(task.OutputFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            while (DateTime.UtcNow < deadline && !File.Exists(task.StopFile) && ParentAlive(task.ParentPid))
            {
                Thread.Sleep(1000);
                var presents = capture.Drain();
                var line = new FrameLine { T = started.Elapsed.TotalSeconds };
                foreach (var group in presents.GroupBy(p => p.pid))
                {
                    string name = NameOf(group.Key, names);
                    if (Ignore.Contains(name))
                    {
                        continue;
                    }

                    var second = new FrameSecond { Pid = group.Key, Name = name, Frames = group.Count() };
                    long previous = lastQpc.TryGetValue(group.Key, out long q) ? q : 0;
                    foreach (var (_, qpc) in group.OrderBy(g => g.qpc))
                    {
                        if (previous > 0 && qpc > previous)
                        {
                            float ms = (float)((qpc - previous) * 1000.0 / EtwPresentCapture.Frequency);
                            if (ms < 5000)
                            {
                                second.FrameTimesMs.Add(ms);
                            }
                        }

                        previous = qpc;
                    }

                    lastQpc[group.Key] = previous;
                    line.P.Add(second);
                }

                writer.WriteLine(JsonSerializer.Serialize(line));
            }

            capture.Stop();
            return new ElevatedResult { Success = true, Message = L.T("Capture finished.") };
        }
        catch (Exception ex)
        {
            AppLog.Error("Frame capture failed", ex);
            return new ElevatedResult { Success = false, Message = L.T("Frame capture failed: ") + ex.Message };
        }
    }

    private static bool ParentAlive(int pid)
    {
        if (pid <= 0)
        {
            return true;
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string NameOf(int pid, Dictionary<int, string> cache)
    {
        if (cache.TryGetValue(pid, out string? name))
        {
            return name;
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch
        {
            name = "pid " + pid;
        }

        cache[pid] = name;
        return name;
    }
}
