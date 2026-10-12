using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

public sealed record UpdateInfo(Version Version, string Tag, string PageUrl, string? InstallerUrl, string Notes);

/// <summary>
/// Checks GitHub Releases for a newer ConstiBoost (one anonymous HTTPS GET – no personal or system data is sent).
/// Can be turned off in Settings. Installs only after the user confirms.
/// </summary>
public sealed class UpdateService : ObservableObject
{
    public const string Repository = "constantinnickchen-del/hone-klon";
    private UpdateInfo? _available;
    private string _status = L.T("Not checked yet");

    public static UpdateService Instance { get; } = new();

    public static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(2, 2, 0);

    public UpdateInfo? Available { get => _available; private set => SetProperty(ref _available, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public async Task CheckOnStartupAsync()
    {
        if (!SettingsService.Instance.Current.CheckForUpdates)
        {
            Status = L.T("Automatic update check is off");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(20));
        await CheckAsync(false);
    }

    public async Task CheckAsync(bool userInitiated)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ConstiBoost-UpdateCheck");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (!response.IsSuccessStatusCode)
            {
                Status = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? L.T("No public release found yet (the repository may be private during development).")
                    : L.F("Update check failed ({0}).", (int)response.StatusCode);
                return;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "v0";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            {
                Status = L.T("Release version could not be read.");
                return;
            }

            string? installer = root.TryGetProperty("assets", out var assets)
                ? assets.EnumerateArray().Select(a => (name: a.GetProperty("name").GetString() ?? string.Empty, url: a.GetProperty("browser_download_url").GetString()))
                    .FirstOrDefault(a => a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && a.name.Contains("Setup", StringComparison.OrdinalIgnoreCase)).url
                : null;
            var info = new UpdateInfo(version, tag, root.GetProperty("html_url").GetString() ?? string.Empty, installer,
                root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty);
            if (Normalize(version) > Normalize(CurrentVersion))
            {
                Available = info;
                Status = L.F("Update available: {0} (installed: {1})", tag, CurrentVersion.ToString(3));
                NotificationService.Instance.Show(L.F("⬆️ ConstiBoost {0} is available – Settings → Updates.", tag), NotificationKind.Info);
            }
            else
            {
                Available = null;
                Status = L.F("ConstiBoost is up to date ({0}).", CurrentVersion.ToString(3));
                if (userInitiated)
                {
                    NotificationService.Instance.Show(Status, NotificationKind.Success);
                }
            }
        }
        catch (Exception ex)
        {
            Status = L.T("Update check not possible: ") + Safe.Describe(ex);
        }
    }

    public async Task InstallAsync(IProgress<string> progress)
    {
        var info = Available;
        if (info is null)
        {
            return;
        }

        if (info.InstallerUrl is null)
        {
            Process.Start(new ProcessStartInfo(info.PageUrl) { UseShellExecute = true });
            return;
        }

        try
        {
            string target = Path.Combine(AppPaths.Temp, $"ConstiBoost-Setup-{info.Tag}.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ConstiBoost-Update");
            using (var response = await http.GetAsync(info.InstallerUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var file = File.Create(target);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read));
                    done += read;
                    progress.Report(total > 0 ? L.F("Downloading… {0} %", done * 100 / total) : L.F("Downloading… {0}", Formatting.Bytes(done)));
                }
            }

            ActionLog.Success("Update", CurrentVersion.ToString(3), info.Tag + " (installer started)");
            Process.Start(new ProcessStartInfo(target, "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS") { UseShellExecute = true });
            TrayService.Instance.Exit();
        }
        catch (Exception ex)
        {
            AppLog.Error("Update failed", ex);
            NotificationService.Instance.Show(L.T("The update couldn't be downloaded: ") + Safe.Describe(ex), NotificationKind.Error);
        }
    }

    private static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
