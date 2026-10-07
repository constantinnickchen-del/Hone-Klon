using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.WinSys;

namespace ConstiBoost.Hardware;

public sealed class BenchmarkResult
{
    public DateTime Timestamp { get; set; }
    public string Label { get; set; } = string.Empty;
    public double SingleThreadMops { get; set; }
    public double MultiThreadMops { get; set; }
    public double MemoryGBps { get; set; }
    public double BackgroundCpuPercent { get; set; }
    public double SchedulingJitterMs { get; set; }
    public int Score { get; set; }

    public string TimeText => Timestamp.ToString("dd.MM.yyyy HH:mm");
}

/// <summary>
/// "System Performance Benchmark": measures CPU, memory and system responsiveness on this PC.
/// It is NOT a game/FPS benchmark – scores are only meaningful when comparing runs on the same PC.
/// </summary>
public static class BenchmarkService
{
    // Reference values define 1000 points per category (roughly a mid-range 2021 laptop).
    private const double RefSingle = 900;
    private const double RefMulti = 6000;
    private const double RefMemory = 12;

    public static async Task<BenchmarkResult> RunAsync(string label, IProgress<(int percent, string step)> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            var result = new BenchmarkResult { Timestamp = DateTime.Now, Label = label };

            progress.Report((5, "Measuring background load…"));
            result.BackgroundCpuPercent = MeasureBackgroundLoad(token);

            progress.Report((25, "CPU single-thread…"));
            result.SingleThreadMops = RunCpu(1, TimeSpan.FromSeconds(3), token);

            progress.Report((50, "CPU multi-thread…"));
            result.MultiThreadMops = RunCpu(Environment.ProcessorCount, TimeSpan.FromSeconds(3), token);

            progress.Report((75, "Memory bandwidth…"));
            result.MemoryGBps = RunMemory(TimeSpan.FromSeconds(2), token);

            progress.Report((90, "System responsiveness…"));
            result.SchedulingJitterMs = MeasureJitter(token);

            double s = result.SingleThreadMops / RefSingle * 1000;
            double m = result.MultiThreadMops / RefMulti * 1000;
            double mem = result.MemoryGBps / RefMemory * 1000;
            double responsiveness = Math.Clamp(1000 - result.SchedulingJitterMs * 150 - result.BackgroundCpuPercent * 10, 0, 1000);
            result.Score = (int)Math.Round(0.3 * s + 0.3 * m + 0.2 * mem + 0.2 * responsiveness);
            progress.Report((100, "Done"));
            return result;
        }, token);
    }

    private static double MeasureBackgroundLoad(CancellationToken token)
    {
        var sampler = new CpuUsageSampler();
        sampler.Sample();
        var samples = new List<double>();
        for (int i = 0; i < 6; i++)
        {
            token.ThrowIfCancellationRequested();
            Thread.Sleep(500);
            if (sampler.Sample() is double v)
            {
                samples.Add(v);
            }
        }

        return samples.Count == 0 ? 0 : samples.Average();
    }

    private static double RunCpu(int threads, TimeSpan duration, CancellationToken token)
    {
        long totalOps = 0;
        var sw = Stopwatch.StartNew();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = token }, _ =>
        {
            long ops = 0;
            ulong x = 88172645463325252UL;
            double f = 1.0001;
            var local = Stopwatch.StartNew();
            while (local.Elapsed < duration)
            {
                for (int i = 0; i < 10_000; i++)
                {
                    x ^= x << 13;
                    x ^= x >> 7;
                    x ^= x << 17;
                    f = f * 1.0000001 + (x & 0xFF) * 1e-9;
                }

                ops += 10_000;
            }

            if (f < 0)
            {
                ops++; // keeps the JIT from removing the loop
            }

            Interlocked.Add(ref totalOps, ops);
        });
        sw.Stop();
        return totalOps / sw.Elapsed.TotalSeconds / 1_000_000;
    }

    private static double RunMemory(TimeSpan duration, CancellationToken token)
    {
        const int size = 64 * 1024 * 1024;
        var source = new byte[size];
        var target = new byte[size];
        new Random(42).NextBytes(source.AsSpan(0, 4096));
        long copied = 0;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            Buffer.BlockCopy(source, 0, target, 0, size);
            copied += size;
        }

        sw.Stop();
        return copied / sw.Elapsed.TotalSeconds / 1_000_000_000;
    }

    private static double MeasureJitter(CancellationToken token)
    {
        var deviations = new List<double>();
        var sw = new Stopwatch();
        for (int i = 0; i < 100; i++)
        {
            token.ThrowIfCancellationRequested();
            sw.Restart();
            Thread.Sleep(5);
            sw.Stop();
            deviations.Add(Math.Abs(sw.Elapsed.TotalMilliseconds - 5));
        }

        deviations.Sort();
        // Use the 90th percentile: spikes matter for smoothness.
        return deviations[(int)(deviations.Count * 0.9)];
    }

    public static List<BenchmarkResult> LoadHistory()
    {
        try
        {
            if (File.Exists(Core.AppPaths.BenchmarkFile))
            {
                return JsonSerializer.Deserialize<List<BenchmarkResult>>(File.ReadAllText(Core.AppPaths.BenchmarkFile)) ?? new();
            }
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Benchmark history unreadable: " + ex.Message);
        }

        return new();
    }

    public static void SaveHistory(IEnumerable<BenchmarkResult> results)
    {
        try
        {
            File.WriteAllText(Core.AppPaths.BenchmarkFile, JsonSerializer.Serialize(results.TakeLast(50).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Benchmark history could not be saved: " + ex.Message);
        }
    }
}
