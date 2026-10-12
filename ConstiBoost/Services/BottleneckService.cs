using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Games;
using ConstiBoost.Hardware;
using ConstiBoost.Localization;
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
    SoftwareRendering,
    OldDriver,
}

public sealed record BottleneckFinding(BottleneckKind Kind, string Title, string Explanation, IReadOnlyList<string> Fixes)
{
    // Fixed English texts are translated here; texts with values use L.F where they are built.
    public string Title { get; init; } = L.T(Title);
    public string Explanation { get; init; } = L.T(Explanation);
    public IReadOnlyList<string> Fixes { get; init; } = Fixes.Select(L.T).ToList();
}

public sealed record BottleneckReport(
    double? GpuAverage,
    double CpuAverage,
    double BusiestCoreAverage,
    double? TemperatureMax,
    double? CpuMhzAverage,
    double? Fps,
    int? RefreshRate,
    bool OnBattery,
    IReadOnlyList<BottleneckFinding> Findings,
    double? GameGpu = null,
    double? GameCpuCores = null)
{
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                GpuAverage is double g ? $"GPU {g:0} %" : L.T("GPU: sensor unavailable"),
                L.F("CPU {0:0} % (busiest thread {1:0} %)", CpuAverage, BusiestCoreAverage),
            };
            if (TemperatureMax is double t)
            {
                parts.Add(L.F("max {0:0} °C", t));
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
                parts.Add(L.F("display {0} Hz", hz));
            }

            if (OnBattery)
            {
                parts.Add(L.T("on battery"));
            }

            if (GameGpu is double gg)
            {
                parts.Add(L.F("game GPU {0:0} %", gg));
            }

            if (GameCpuCores is double cc)
            {
                parts.Add(L.F("game CPU {0:0} % of one thread", cc * 100));
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
    public static async Task<BottleneckReport> AnalyzeAsync(int seconds, IProgress<string> progress, CancellationToken token, GameInfo? game = null)
    {
        var gameGpu = new List<double>();
        var gameCpuStart = new Dictionary<int, TimeSpan>();
        var gameCpuEnd = new Dictionary<int, TimeSpan>();
        int? browserGpuPid = null;
        var stopwatch = Stopwatch.StartNew();
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
                var perProcessGpu = new GpuUsageSampler();
                perProcessGpu.Sample();
                if (game != null)
                {
                    ReadCpuTimes(game, gameCpuStart);
                    if (game.IsBrowser)
                    {
                        browserGpuPid = BrowserProcessInfo.CommandLines(gameCpuStart.Keys)
                            .Where(kv => kv.Value.Contains("--type=gpu-process", StringComparison.OrdinalIgnoreCase))
                            .Select(kv => (int?)kv.Key).FirstOrDefault();
                    }
                }

                stopwatch.Restart();
                for (int i = 0; i < seconds; i++)
                {
                    token.ThrowIfCancellationRequested();
                    progress.Report(L.F("Analyzing… {0}/{1} s – keep playing the game in the meantime.", i + 1, seconds));
                    await Task.Delay(1000, token);
                    var values = cores.Sample();
                    if (values.Length > 0)
                    {
                        coreSamples.Add(values);
                    }

                    if (game != null && perProcessGpu.IsAvailable)
                    {
                        perProcessGpu.Sample();
                        var pids = gameCpuStart.Keys;
                        double usage = perProcessGpu.PerProcess.Where(kv => pids.Contains(kv.Key)).Select(kv => kv.Value).DefaultIfEmpty(0).Max();
                        gameGpu.Add(usage);
                    }
                }

                if (game != null)
                {
                    ReadCpuTimes(game, gameCpuEnd);
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

        double elapsedMs = Math.Max(1, stopwatch.Elapsed.TotalMilliseconds);
        double? gameGpuAvg = gameGpu.Count > 0 ? gameGpu.Average() : null;
        double? gameCpuCores = null;
        double? gpuProcessCores = null;
        if (game != null && gameCpuEnd.Count > 0)
        {
            double total = 0;
            foreach (var (pid, end) in gameCpuEnd)
            {
                if (gameCpuStart.TryGetValue(pid, out var start))
                {
                    double delta = (end - start).TotalMilliseconds / elapsedMs;
                    total += delta;
                    if (pid == browserGpuPid)
                    {
                        gpuProcessCores = delta;
                    }
                }
            }

            gameCpuCores = total;
        }

        int? refresh = ReadRefreshRate();
        bool onBattery = Safe(() => System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline);
        var findings = Evaluate(gpu, cpuAvg, busiest, tMax, fps, refresh, onBattery);
        AddGameFindings(findings, game, gameGpuAvg, gameCpuCores, gpuProcessCores);
        var report = new BottleneckReport(gpu, cpuAvg, busiest, tMax, mhzAvg, fps, refresh, onBattery, findings, gameGpuAvg, gameCpuCores);
        AppLog.Info("Bottleneck analysis: " + report.Summary + " → " + string.Join(", ", findings.Select(f => f.Kind)));
        return report;
    }

    private static List<BottleneckFinding> Evaluate(double? gpu, double cpuAvg, double busiest, double? tMax, double? fps, int? refresh, bool onBattery)
    {
        var list = new List<BottleneckFinding>();

        if (fps is double f && refresh is int hz && Math.Abs(f - hz) <= Math.Max(3, hz * 0.05))
        {
            list.Add(new BottleneckFinding(BottleneckKind.FrameCap, L.T("FPS are capped at your refresh rate"),
                L.F("The game delivers ~{0:0} FPS = your display's {1} Hz. The GPU does not need to work harder – this is normal and not a problem (VSync / frame limit).", f, hz),
                new[]
                {
                    L.T("Nothing to fix: more FPS than Hz are not visible on this display."),
                    L.T("Optional: disable VSync / raise the frame limit in the game for slightly lower input lag (more heat)."),
                    L.T("Browser games: the browser always caps at the refresh rate unless 'Unlock FPS' is used in the Performance Launch."),
                }));
        }

        if (gpu is double g && g < 75 && busiest >= 85)
        {
            list.Add(new BottleneckFinding(BottleneckKind.CpuSingleThread, L.T("CPU limit (one thread is maxed out)"),
                L.F("One CPU thread runs at ~{0:0} % while the GPU only reaches ~{1:0} %. The game (or the browser's JavaScript) cannot prepare frames faster, so the GPU waits. " +
                L.T("Total CPU usage looks low because the other cores are idle – games mostly scale with one fast core."), busiest, g),
                new[]
                {
                    L.T("Start Game Focus with the 'Max FPS' profile (Best performance power mode = higher boost clocks)."),
                    L.T("Plug in the charger – laptops reduce the CPU boost heavily on battery."),
                    L.T("Close background apps (Game Focus does this) – every bit of CPU time helps the main thread."),
                    L.T("Lower CPU-heavy settings: view distance, NPC/traffic density, shadows, physics. Resolution barely helps here."),
                    L.T("Browser games: close other tabs and use the Performance Launch (clean profile, no extensions)."),
                }));
        }
        else if (gpu is double g2 && g2 >= 88)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Gpu, L.T("GPU limit (GPU is fully used)"),
                L.F("The GPU works at ~{0:0} % – it is the limit, and that is the ideal state. More FPS only come from fewer pixels or lower graphics quality.", g2),
                new[]
                {
                    L.T("Lower the resolution / render scale (biggest effect on integrated graphics)."),
                    L.T("Reduce shadows, effects, anti-aliasing and post-processing."),
                    L.T("Use AMD FSR / resolution scaling if the game offers it."),
                    L.T("Browser games: Performance Launch with a smaller render size."),
                    L.T("Integrated graphics use the RAM: dual-channel RAM (2 modules) gives up to +50 % more GPU performance."),
                }));
        }
        else if (gpu is double g3 && busiest < 70 && g3 < 60)
        {
            list.Add(new BottleneckFinding(BottleneckKind.NotLoaded, L.T("Neither CPU nor GPU is maxed out"),
                L.T("Both have reserve. Usually the game is waiting for something: a frame limit, loading/streaming, the network (online games/servers) or simply a light scene. If the game was not in the foreground during the test, run it again while playing."),
                new[]
                {
                    L.T("Check the in-game FPS limit / VSync."),
                    L.T("Run the analysis again while actively playing (game window in focus)."),
                    L.T("Browser games: a background tab is throttled by the browser – keep the game tab visible."),
                }));
        }

        if (tMax is double t && t >= 90)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Thermal, L.T("Possible thermal throttling"),
                L.F("The temperature reached {0:0} °C. Above ~90-95 °C the Ryzen CPU lowers its clock to protect itself (this protection is never disabled by ConstiBoost).", t),
                new[]
                {
                    L.T("Put the laptop on a hard, flat surface or use a cooling pad; keep the vents free."),
                    L.T("Use the Thermal page / 'Cool' profile: slightly lower max clock = much less heat, often more stable FPS."),
                    L.T("Cap the FPS slightly below what the game reaches – less heat, fewer drops."),
                    L.T("Cleaning dust from the fan helps a lot on older laptops."),
                }));
        }

        if (onBattery)
        {
            list.Add(new BottleneckFinding(BottleneckKind.Battery, L.T("Running on battery"),
                L.T("On battery Windows and the laptop firmware limit CPU and GPU power. This alone can cost 30-50 % FPS."),
                new[] { "Plug in the charger while gaming." }));
        }

        return list;
    }

    private static void ReadCpuTimes(GameInfo game, Dictionary<int, TimeSpan> target)
    {
        foreach (var p in GameFocusService.FindGameProcesses(game.ProcessRoots))
        {
            using (p)
            {
                try
                {
                    target[p.Id] = p.TotalProcessorTime;
                }
                catch
                {
                    // protected / ended
                }
            }
        }
    }

    private static void AddGameFindings(List<BottleneckFinding> list, GameInfo? game, double? gameGpu, double? gameCpuCores, double? gpuProcessCores)
    {
        if (game is null)
        {
            return;
        }

        if (game.IsBrowser && gameGpu is double gg && gg < 12 && (gameCpuCores ?? 0) > 0.5)
        {
            string gpuProc = gpuProcessCores is double gp ? " " + L.F("The browser's GPU process alone uses {0:0} % of a CPU thread.", gp * 100) : string.Empty;
            list.Insert(0, new BottleneckFinding(BottleneckKind.SoftwareRendering, "⚠ The web game is rendered by the CPU, not the graphics chip",
                L.F("The browser uses only ~{0:0} % of the GPU but ~{1:0} % of a CPU thread.{2} This is the typical sign of software rendering (WebGL without hardware acceleration) – it causes single-digit FPS. Fixing this brings by far the biggest gain.", gg, gameCpuCores * 100, gpuProc),
                new[]
                {
                    L.T("Browser settings → System → 'Use graphics acceleration when available' ON, then restart the browser."),
                    L.T("Open the flags page shown in the Browser Game Boost card: 'Override software rendering list' → Enabled → Relaunch."),
                    L.T("Use ⚡ PERFORMANCE LAUNCH with 'restart browser' – it starts the browser with the GPU block list disabled."),
                    L.T("Update the AMD graphics driver (AMD Adrenalin / amd.com) – old OEM drivers are often blocked by browsers."),
                    "Check the GPU page (chrome://gpu): 'WebGL: Hardware accelerated' must be shown.",
                }));
        }
        else if (game.IsBrowser && gameCpuCores is double cores && cores >= 0.85 && gameGpu is double g2 && g2 < 70)
        {
            list.Insert(0, new BottleneckFinding(BottleneckKind.CpuSingleThread, L.T("The web game is limited by its JavaScript/WebAssembly thread"),
                L.F("The browser uses ~{0:0} % of a CPU thread and the GPU only ~{1:0} %. Web ports of PC games run their game logic on ONE browser thread – that thread is the limit, not the graphics. Fewer pixels help only a little here.", cores * 100, g2),
                new[]
                {
                    L.T("Charger in + Game Focus 'Max FPS' (highest CPU boost clock)."),
                    L.T("Close every other tab and window – they compete for the same CPU."),
                    L.T("In the game's own options: lower draw distance, traffic/pedestrian density, shadows (if it has them)."),
                    L.T("Try another Chromium browser (Edge/Chrome) – JavaScript speed differs between browsers and versions."),
                }));
        }

        var driver = ScanService.Instance.Last?.Hardware.Gpus.FirstOrDefault()?.DriverDate;
        if (driver is DateTime date && date < DateTime.Now.AddMonths(-18))
        {
            list.Add(new BottleneckFinding(BottleneckKind.OldDriver, L.T("Old graphics driver"),
                L.F("Your graphics driver is from {0:MMMM yyyy}. Old drivers are slower in games and browsers often block WebGL acceleration on them.", date),
                new[] { "Install the current AMD Software: Adrenalin Edition (amd.com → Drivers → Ryzen 7 5700U / Radeon Graphics). A restore point is created by the installer." }));
        }
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
