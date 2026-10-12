using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>
/// Optional weekly maintenance while ConstiBoost runs (tray): removes old temp files and crash reports (user folders only,
/// no admin, no shader caches) and checks startup apps, free disk space and driver age. Results are shown and logged.
/// </summary>
public sealed class MaintenanceService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(1) };

    public static MaintenanceService Instance { get; } = new();

    public void Start()
    {
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
        _ = Task.Delay(TimeSpan.FromMinutes(2)).ContinueWith(_ => System.Windows.Application.Current?.Dispatcher.BeginInvoke(async () => await CheckAsync()));
    }

    private async Task CheckAsync()
    {
        var s = SettingsService.Instance.Current;
        if (!s.WeeklyMaintenance || (s.LastMaintenance is DateTime last && DateTime.Now - last < TimeSpan.FromDays(7)))
        {
            return;
        }

        if (GamingModeService.Instance.IsActive || Games.GameFocusService.Instance.IsActive)
        {
            return; // never while gaming
        }

        await RunAsync(silent: true);
    }

    public async Task<string> RunAsync(bool silent)
    {
        var report = await Task.Run(() =>
        {
            long freed = 0;
            foreach (var category in CleanerService.GetCategories().Where(c => c.Id is "temp" or "crash"))
            {
                foreach (string path in category.Paths)
                {
                    freed += CleanerService.DeleteContents(path, Math.Max(category.MinAgeHours, 24)).FreedBytes;
                }
            }

            var parts = new System.Collections.Generic.List<string> { L.F("{0} of temp files and crash reports removed", Formatting.Bytes(freed)) };
            int startup = Safe.Try(() => StartupService.GetEntries().Count(e => e.IsEnabled && e.IsKnownNonEssential), 0);
            if (startup > 0)
            {
                parts.Add(L.F("{0} non-essential startup app(s) enabled – see Startup", startup));
            }

            var system = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\");
            if (system.IsReady && system.TotalSize > 0 && 100.0 * system.AvailableFreeSpace / system.TotalSize < 12)
            {
                parts.Add(L.F("system drive almost full ({0} free)", Formatting.Bytes(system.AvailableFreeSpace)));
            }

            var driver = ScanService.Instance.Last?.Hardware.Gpus.FirstOrDefault()?.DriverDate;
            if (driver is DateTime d && DateTime.Now - d > TimeSpan.FromDays(180))
            {
                parts.Add(L.F("graphics driver from {0:MM/yyyy} – an update may improve game performance", d));
            }

            return string.Join(" · ", parts);
        });

        SettingsService.Instance.Current.LastMaintenance = DateTime.Now;
        SettingsService.Instance.Save();
        ActionLog.Success("Weekly maintenance", null, report);
        string message = L.T("🧹 Maintenance done: ") + report;
        NotificationService.Instance.Show(message, NotificationKind.Success);
        if (silent)
        {
            TrayService.Instance.ShowBalloon(L.T("ConstiBoost maintenance"), report);
        }

        return report;
    }
}
