using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;

namespace ConstiBoost.Services;

/// <summary>🌡️ 60 SECOND THERMAL TEST – real measurements only, stored per Gaming Mode profile for comparison.</summary>
public static class ThermalTestService
{
    public static async Task<ThermalTestRecord> RunAsync(int seconds, IProgress<(int s, int total)> progress, CancellationToken token)
    {
        var measurement = await ThermalAnalysisService.MeasureAsync(seconds, progress, token);
        var profile = GamingModeService.Instance.ActiveProfile;
        var record = new ThermalTestRecord
        {
            Time = DateTime.Now,
            Profile = profile is null ? "Normal (no Gaming Mode)" : GamingModeService.ProfileName(profile.Value),
            StartTemperature = measurement.StartTemperature,
            AverageTemperature = measurement.AverageTemperature,
            MaxTemperature = measurement.MaxTemperature,
            FinalTemperature = measurement.FinalTemperature,
            AverageCpuLoad = measurement.AverageCpuLoad,
            AverageCpuMhz = measurement.AverageCpuMhz,
            GameRunning = ThermalMonitorService.Instance.RunningGame != null,
            Source = measurement.Source,
        };

        var history = Load();
        history.Add(record);
        Save(history);
        AppLog.Info($"Thermal test ({record.Profile}): avg {record.AverageTemperature?.ToString("0.0") ?? "n/a"} °C, load {record.AverageCpuLoad?.ToString("0") ?? "n/a"} %");
        return record;
    }

    public static List<ThermalTestRecord> Load()
    {
        try
        {
            if (File.Exists(AppPaths.ThermalTestsFile))
            {
                return JsonSerializer.Deserialize<List<ThermalTestRecord>>(File.ReadAllText(AppPaths.ThermalTestsFile)) ?? new();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Thermal test history unreadable: " + ex.Message);
        }

        return new();
    }

    public static void Save(List<ThermalTestRecord> records)
    {
        try
        {
            File.WriteAllText(AppPaths.ThermalTestsFile, JsonSerializer.Serialize(records.TakeLast(100).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Thermal test history could not be saved: " + ex.Message);
        }
    }
}
