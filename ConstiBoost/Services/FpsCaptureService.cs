using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

/// <summary>
/// Client side of the real FPS capture. Starts the elevated helper once (one UAC prompt), tails its output
/// and offers live per-second values (overlay) and timed measurements (benchmarks).
/// </summary>
public sealed class FpsCaptureService
{
    private readonly object _gate = new();
    private readonly List<FrameLine> _lines = new();
    private Process? _helper;
    private string? _output;
    private string? _stop;
    private long _position;
    private Timer? _tail;
    private int _users;

    public static FpsCaptureService Instance { get; } = new();

    public bool IsRunning => _helper != null && (ReferenceEquals(_helper, Process.GetCurrentProcess()) || !SafeExited(_helper));

    /// <summary>Raised once per second with all processes that presented frames.</summary>
    public event Action<FrameLine>? LineReceived;

    public string? LastError { get; private set; }

    /// <summary>Starts (or joins) the capture. Returns false if the user declined UAC or capture failed.</summary>
    public bool Acquire()
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                _users++;
                return true;
            }

            string id = Guid.NewGuid().ToString("N");
            _output = Path.Combine(AppPaths.Temp, $"frames-{id}.jsonl");
            _stop = Path.Combine(AppPaths.Temp, $"frames-{id}.stop");
            _position = 0;
            _lines.Clear();
            _helper = ElevationService.StartDetached(new ElevatedTask
            {
                Operation = ElevatedOperations.FrameCapture,
                OutputFile = _output,
                StopFile = _stop,
                ParentPid = Environment.ProcessId,
                DurationSeconds = 0,
            }, out string? error);
            if (_helper is null)
            {
                LastError = error;
                return false;
            }

            _users = 1;
            _tail = new Timer(_ => Tail(), null, 500, 500);
            ActionLog.Success("FPS capture", "Off", "Started (ETW)");
            return true;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            _users = Math.Max(0, _users - 1);
            if (_users > 0)
            {
                return;
            }

            try
            {
                if (_stop != null)
                {
                    File.WriteAllText(_stop, "stop");
                }
            }
            catch
            {
                // ignore
            }

            _tail?.Dispose();
            _tail = null;
            _helper = null;
            string? output = _output;
            _ = Task.Delay(4000).ContinueWith(_ =>
            {
                Safe.Try(() => File.Delete(output!));
                Safe.Try(() => File.Delete(_stop!));
            });
        }
    }

    private int _tailBusy;

    private void Tail()
    {
        if (Interlocked.Exchange(ref _tailBusy, 1) == 1)
        {
            return;
        }

        try
        {
            if (_output is null || !File.Exists(_output))
            {
                if (_helper != null && !ReferenceEquals(_helper, Process.GetCurrentProcess()) && SafeExited(_helper))
                {
                    LastError = "The FPS capture helper stopped unexpectedly.";
                }

                return;
            }

            using var stream = new FileStream(_output, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length <= _position)
            {
                return;
            }

            stream.Seek(_position, SeekOrigin.Begin);
            var buffer = new byte[stream.Length - _position];
            int read = stream.Read(buffer, 0, buffer.Length);
            int lastNewline = Array.LastIndexOf(buffer, (byte)'\n', Math.Max(0, read - 1));
            if (lastNewline < 0)
            {
                return; // no complete line yet
            }

            _position += lastNewline + 1;
            string text = System.Text.Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
            foreach (string raw in text.Split('\n'))
            {
                string json = raw.Trim();
                if (json.Length == 0)
                {
                    continue;
                }

                var line = JsonSerializer.Deserialize<FrameLine>(json);
                if (line != null)
                {
                    lock (_gate)
                    {
                        _lines.Add(line);
                        if (_lines.Count > 4 * 3600)
                        {
                            _lines.RemoveRange(0, 600);
                        }
                    }

                    LineReceived?.Invoke(line);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("FPS tail: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _tailBusy, 0);
        }
    }

    /// <summary>
    /// Waits until a matching process presents frames, then measures for <paramref name="seconds"/> seconds.
    /// <paramref name="matcher"/> receives the process path (null if unknown) and name.
    /// </summary>
    public async Task<FpsResult?> MeasureAsync(int seconds, Func<string?, string, bool>? matcher, IProgress<string> progress, CancellationToken token)
    {
        if (!Acquire())
        {
            return null;
        }

        var pathCache = new Dictionary<int, string?>();
        bool Matches(FrameSecond s)
        {
            if (matcher is null)
            {
                return true;
            }

            if (!pathCache.TryGetValue(s.Pid, out string? path))
            {
                path = ProcessSafety.TryGetPath(s.Pid);
                pathCache[s.Pid] = path;
            }

            return matcher(path, s.Name);
        }

        var collected = new List<FrameSecond>();
        int? target = null;
        string targetName = string.Empty;
        var waitStart = DateTime.UtcNow;
        var tcs = new TaskCompletionSource<bool>();
        void OnLine(FrameLine line)
        {
            var candidates = line.P.Where(p => p.Frames >= 3 && Matches(p)).ToList();
            if (target is null)
            {
                var best = candidates.OrderByDescending(c => c.Frames).FirstOrDefault();
                if (best != null)
                {
                    target = best.Pid;
                    targetName = best.Name;
                }
            }

            if (target is int pid)
            {
                var entry = line.P.FirstOrDefault(p => p.Pid == pid) ?? new FrameSecond { Pid = pid, Name = targetName };
                lock (collected)
                {
                    collected.Add(entry);
                    progress.Report($"Measuring {targetName}… {collected.Count}/{seconds} s · {entry.Frames} FPS");
                    if (collected.Count >= seconds)
                    {
                        tcs.TrySetResult(true);
                    }
                }
            }
            else
            {
                progress.Report($"Waiting for the game to render frames… ({(int)(DateTime.UtcNow - waitStart).TotalSeconds} s) – start or switch to the game now.");
                if (DateTime.UtcNow - waitStart > TimeSpan.FromMinutes(5))
                {
                    tcs.TrySetResult(false);
                }
            }
        }

        LineReceived += OnLine;
        try
        {
            using (token.Register(() => tcs.TrySetCanceled()))
            {
                bool ok = await tcs.Task;
                if (!ok)
                {
                    LastError = "No frames from the game were detected. Only DirectX games can be measured (OpenGL/Vulkan are not supported yet).";
                    return null;
                }
            }

            lock (collected)
            {
                return Compute(targetName, collected);
            }
        }
        finally
        {
            LineReceived -= OnLine;
            Release();
        }
    }

    public static FpsResult Compute(string name, List<FrameSecond> seconds)
    {
        var frameTimes = seconds.SelectMany(s => s.FrameTimesMs).Where(f => f > 0).Select(f => (double)f).OrderBy(f => f).ToList();
        int frames = seconds.Sum(s => s.Frames);
        double duration = Math.Max(1, seconds.Count);
        double Percentile(double p) => frameTimes.Count == 0 ? 0 : frameTimes[Math.Min(frameTimes.Count - 1, (int)Math.Ceiling(p * frameTimes.Count) - 1)];
        double median = Percentile(0.5);
        double avgFps = frameTimes.Count > 0 ? 1000.0 / frameTimes.Average() : frames / duration;
        double low1 = Percentile(0.99) > 0 ? 1000.0 / Percentile(0.99) : 0;
        double low01 = Percentile(0.999) > 0 ? 1000.0 / Percentile(0.999) : 0;
        int stutters = median > 0 ? frameTimes.Count(f => f > Math.Max(median * 2.5, median + 20)) : 0;
        var perSecond = seconds.Select(s => (double)s.Frames).ToList();
        return new FpsResult
        {
            ProcessName = name,
            Seconds = duration,
            Frames = frames,
            AverageFps = avgFps,
            Low1 = low1,
            Low01 = low01,
            MinFps = perSecond.Count > 0 ? perSecond.Min() : 0,
            MaxFps = perSecond.Count > 0 ? perSecond.Max() : 0,
            Stutters = stutters,
            MedianFrameTimeMs = median,
            PerSecond = perSecond,
        };
    }

    private static bool SafeExited(Process p)
    {
        try
        {
            return p.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Persistent FPS history (per game) used for before/after, profile comparison and estimate calibration.</summary>
public static class FpsHistory
{
    private static string FilePath => Path.Combine(AppPaths.Root, "fps-results.json");

    public static List<FpsRecord> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<List<FpsRecord>>(File.ReadAllText(FilePath)) ?? new();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("FPS history unreadable: " + ex.Message);
        }

        return new();
    }

    public static void Add(FpsRecord record)
    {
        var all = Load();
        all.Add(record);
        Safe.Try(() => File.WriteAllText(FilePath, JsonSerializer.Serialize(all.TakeLast(500).ToList(), new JsonSerializerOptions { WriteIndented = true })), "Save FPS history");
    }

    public static List<FpsRecord> ForGame(string gameId) => Load().Where(r => r.GameId == gameId).OrderByDescending(r => r.Time).ToList();
}
