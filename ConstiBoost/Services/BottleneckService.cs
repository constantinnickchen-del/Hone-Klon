using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public enum BottleneckKind
{
    CpuSingleThread,
    Gpu,
    FrameCap,
    Thermal,
    Battery,
    NotLoaded,
}

public sealed record BottleneckFinding(BottleneckKind Kind, string Title, string Explanation, IReadOnlyList<string> Fixes);

public sealed record BottleneckReport(
    double? GpuAverage,
    double CpuAverage,
    double BusiestCoreAverage,
    double? TemperatureMax,
    double? CpuMhzAverage,
    double? Fps,
    int? RefreshRate,
    bool OnBattery,
    IReadOnlyList<BottleneckFinding> Findings)
{
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                GpuAverage is double g ? $"GPU {g:0} %" : "GPU: sensor unavailable",
                $"CPU {CpuAverage:0} % (busiest thread {BusiestCoreAverage:0} %)",
            };
            if (TemperatureMax is double t)
            {
                parts.Add($"max {t:0} °C");
            }

            if (CpuMhzAverage is double mhz)
            {
                parts.Add($"{mhz / 1000:0.0} GHz");
            }

            if (Fps is double fps)
            {
                parts.Add($"{fps:0} FPS");
            }

            if (RefreshRate is int hz)
            {
                parts.Add($"display {hz} Hz");
            }

            if (OnBattery)
            {
                parts.Add("on battery");
            }

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>
/// "Why is my GPU not at 100 %?" – samples real per-thread CPU load, GPU load, temperature, clock
/// and (if the FPS capture is already running) FPS while the game runs, and explains the limit.
/// Read-only: nothing is changed by the analysis.
/// </summary>
public static class BottleneckService
{
    public static async Task<BottleneckReport> AnalyzeAsync(int seconds, IProgress<string> progress, CancellationToken token)
    {
        var gpuValues = new List<double>();
        var temps = new List<double>();
        var clocks = new List<double>();
        var fpsValues = new List<int>();

        void OnSample(MonitorSnapshot s)
        {
            lock (gpuValues)
            {
                if (s.GpuPercent is double g)
                {
                    gpuValues.Add(g);
                }

                if (s.Sensors.BestTemperature is double t)
                {
                    temps.Add(t);
                }

                if (s.CpuMhz is double mhz)
                {
                    clocks.Add(mhz);
                }
            }
        }

        void OnFrames(FrameLine line)
        {
            var top = line.P.OrderByDescending(p => p.Frames).FirstOrDefault();
            if (top != null && top.Frames > 3)
            {
                lock (fpsValues)
                {
                    fpsValues.Add(top.Frames);
                }
            }
        }

        var monitor = MonitorService.Instance;
        monitor.Subscribe();
        monitor.Sampled += OnSample;
        bool fpsRunning = FpsCaptureService.Instance.IsRunning;
        if (fpsRunning)
        {
            FpsCaptureService.Instance.LineReceived += OnFrames;
        }

        var coreSamples = new List<double[]>();
        try
        {
            await Task.Run(async () =>
            {
                var cores = new PerCoreSampler();
                cores.Sample();
                for (int i = 0; i < seconds; i++)
                {
                    token.ThrowIfCancellationRequested();
                    progress.Report($"Analyzing… {i + 1}/{seconds} s – keep playing the game in the meantime.");
                    await Task.Delay(1000, token);
                    var values = cores.Sample();
                    if (values.Length > 0)
                    {
                        coreSamples.Add(values);
                    }
                }
            }, token);
        }
        finally
        {
            monitor.Sampled -= OnSample;
            monitor.Unsubscribe();
            if (fpsRunning)
            {
                FpsCaptureService.Instance.LineReceived -= OnFrames;
            }
        }

        double cpuAvg = coreSamples.Count > 0 ? coreSamples.Average(s => s.Average()) : 0;
        // A game's main thread jumps between cores; the busiest core per second is the best real proxy.
        double busiest = coreSamples.Count > 0 ? coreSamples.Average(s => s.Max()) : 0;
        double? gpu;
        double? tMax;
        double? mhzAvg;
        lock (gpuValues)
        {
            gpu = gpuValues.Count > 0 ? gpuValues.Average() : null;
            tMax = temps.Count > 0 ? temps.Max() : null;
            mhzAvg = clocks.Count > 0 ? clocks.Average() : null;
        }

        double? fps;
        lock (fpsValues)
        {
            fps = fpsValues.Count > 0 ? fpsValues.Average() : null;
        }

        int? refresh = ReadRefreshRate();
        bool onBattery = Safe(() => System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline);
        var findings = Evaluate(gpu, cpuAvg, busiest, tMax, fps, refresh, onBattery);
        var report = new BottleneckReport(gpu, cpuAvg, busiest, tMax, mhzAvg, fps, refresh, onBattery, findings);
        AppLog.Info("Bottleneck analysis: " + report.Summary + " → " + string.Join(", ", findings.Select(f => f.Kind)));
        return report;
    }

    private static List<BottleneckFinding> Evaluate(double? gpu, double cpuAvg, double busiest, double? tMax, double? fps, int? refresh, bool onBattery)
    {
        var list = new List<BottleneckFinding>();

        if (fps is double f && refresh is int hz && Math.Abs(f - hz) <= Math.Max(3, hz * 0.05))
        {
            list.Add(new BottleneckFinding(BottleneckKind.FrameCap, "FPS are capped at your refresh rate",
                $"The game delivers ~{f:0} FPS = your display's {hz} Hz. The GPU does not need to work harder – this is normal and not a problem (VSync / frame limit).",
                new[]
                {
                    "Nothing to fix: more FPS than Hz are not visible on this display.",
                    "Optional: disable VSync / raise the frame limit in the game for slightly lower input lag (more heat).",
                    "Browser games: the browser always caps at the refresh rate unless 'Unlock FPS' is used in the Performance Launch.",
                }));
        }

        if (gpu is double g && g < 75 && busiest >= 85)
        {
            list.Add(new BottleneckFinding(BottleneckKind.CpuSingleThread, "CPU limit (one thread is maxed out)",
                $"One CPU thread runs at ~{busiest:0} % while the GPU only reaches ~{g:0} %. The game (or the browser's JavaScript) cannot prepare frames faster, so the GPU waits. " +
                "Total CPU usage looks low because the other cores are idle – games mostly scale with one fast core.",
                new[]
                {
                    "Start Game Focus with the 'Max FPS' profile (Best performance power mode = higher boost clocks).",
                    "Plug in the charger – laptops reduce the CPU boost heavily on battery.",
                    "Close background apps (Game Focus does this) – every bit of CPU time helps the main thread.",
                    "Lower CPU-heavy settings: view distance, NPC/traffic density, shadows, physics. Resolution barely helps here.",
                    "Browser games: close other tabs and use the Performance Launch (clean profile, no extensions).",
                }));
        }
        else if (gpu is double g2 && g2 >= 88)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Gpu, "GPU limit (GPU is fully used)",
                $"The GPU works at ~{g2:0} % – it is the limit, and that is the ideal state. More FPS only come from fewer pixels or lower graphics quality.",
                new[]
                {
                    "Lower the resolution / render scale (biggest effect on integrated graphics).",
                    "Reduce shadows, effects, anti-aliasing and post-processing.",
                    "Use AMD FSR / resolution scaling if the game offers it.",
                    "Browser games: Performance Launch with a smaller render size.",
                    "Integrated graphics use the RAM: dual-channel RAM (2 modules) gives up to +50 % more GPU performance.",
                }));
        }
        else if (gpu is double g3 && busiest < 70 && g3 < 60)
        {
            list.Add(new BottleneckFinding(BottleneckKind.NotLoaded, "Neither CPU nor GPU is maxed out",
                "Both have reserve. Usually the game is waiting for something: a frame limit, loading/streaming, the network (online games/servers) or simply a light scene. If the game was not in the foreground during the test, run it again while playing.",
                new[]
                {
                    "Check the in-game FPS limit / VSync.",
                    "Run the analysis again while actively playing (game window in focus).",
                    "Browser games: a background tab is throttled by the browser – keep the game tab visible.",
                }));
        }

        if (tMax is double t && t >= 90)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Thermal, "Possible thermal throttling",
                $"The temperature reached {t:0} °C. Above ~90-95 °C the Ryzen CPU lowers its clock to protect itself (this protection is never disabled by ConstiBoost).",
                new[]
                {
                    "Put the laptop on a hard, flat surface or use a cooling pad; keep the vents free.",
                    "Use the Thermal page / 'Cool' profile: slightly lower max clock = much less heat, often more stable FPS.",
                    "Cap the FPS slightly below what the game reaches – less heat, fewer drops.",
                    "Cleaning dust from the fan helps a lot on older laptops.",
                }));
        }

        if (onBattery)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Battery, "Running on battery",
                "On battery Windows and the laptop firmware limit CPU and GPU power. This alone can cost 30-50 % FPS.",
                new[] { "Plug in the charger while gaming." }));
        }

        return list;
    }

    private static int? ReadRefreshRate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo["CurrentRefreshRate"] is uint hz && hz > 1)
                    {
                        return (int)hz;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Refresh rate unavailable: " + ex.Message);
        }

        return null;
    }

    private static bool Safe(Func<bool> f)
    {
        try
        {
            return f();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Per logical processor "% Processor Time" from two raw samples.</summary>
    private sealed class PerCoreSampler
    {
        private Dictionary<string, CounterSample> _previous = new();

        public double[] Sample()
        {
            try
            {
                var category = new PerformanceCounterCategory("Processor Information");
                var data = category.ReadCategory()["% processor time"];
                if (data is null)
                {
                    return Array.Empty<double>();
                }

                var current = new Dictionary<string, CounterSample>();
                var result = new List<double>();
                foreach (InstanceData instance in data.Values)
                {
                    string name = instance.InstanceName;
                    if (!name.Contains(',') || name.EndsWith("_Total", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    current[name] = instance.Sample;
                    if (_previous.TryGetValue(name, out var prev))
                    {
                        result.Add(Math.Clamp(CounterSample.Calculate(prev, instance.Sample), 0, 100));
                    }
                }

                _previous = current;
                return result.ToArray();
            }
            catch (Exception ex)
            {
                AppLog.Warn("Per-core CPU counter unavailable: " + ex.Message);
                return Array.Empty<double>();
            }
        }
    }
}
