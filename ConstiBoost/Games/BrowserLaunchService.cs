using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using ConstiBoost.Core;
using ConstiBoost.Logging;

namespace ConstiBoost.Games;

/// <summary>Reads the command lines of the browser's own processes (to tell GPU process / renderers apart).</summary>
public static class BrowserProcessInfo
{
    public static Dictionary<int, string> CommandLines(IEnumerable<int> pids)
    {
        var wanted = pids.ToHashSet();
        var result = new Dictionary<int, string>();
        if (wanted.Count == 0)
        {
            return result;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE " +
                string.Join(" OR ", wanted.Select(p => "ProcessId=" + p)));
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    if (mo["ProcessId"] is uint pid && mo["CommandLine"] is string cmd)
                    {
                        result[(int)pid] = cmd;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Browser process info: " + ex.Message);
        }

        return result;
    }
}

public sealed record BrowserLaunchOptions(string Url, int RenderScalePercent, bool UnlockFps, bool SeparateProfile);

/// <summary>
/// ⚡ Performance Launch for web games (Chrome, Edge, Brave, Opera).
/// Default: your normal browser profile (extensions such as your ad blocker keep working) restarted with
/// GPU-friendly start flags – the browser's own "--restore-last-session" brings your tabs back.
/// Optional: a separate clean ConstiBoost profile. Nothing inside the website is read or modified.
/// </summary>
public static class BrowserLaunchService
{
    public static bool IsSupported(GameInfo game) => game.IsBrowser && game.Id != "browser:firefox";

    public static string ProfileDirectory(GameInfo game) =>
        Path.Combine(AppPaths.Root, "BrowserProfile", game.Id.Replace("browser:", string.Empty));

    public static bool TryNormalizeUrl(string? input, out string url)
    {
        url = string.Empty;
        string text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrEmpty(uri.Host))
        {
            url = uri.AbsoluteUri;
            return true;
        }

        return false;
    }

    /// <summary>Running processes of this browser that belong to the normal profile (separate=false) or the ConstiBoost profile (separate=true).</summary>
    public static List<Process> RunningProcesses(GameInfo game, bool separate)
    {
        var all = GameFocusService.FindGameProcesses(game.ProcessRoots);
        if (all.Count == 0)
        {
            return all;
        }

        string marker = ProfileDirectory(game);
        var commandLines = BrowserProcessInfo.CommandLines(all.Select(p => p.Id));
        var result = new List<Process>();
        foreach (var p in all)
        {
            bool isOurs = commandLines.TryGetValue(p.Id, out string? cmd) && cmd.Contains(marker, StringComparison.OrdinalIgnoreCase);
            if (isOurs == separate)
            {
                result.Add(p);
            }
            else
            {
                p.Dispose();
            }
        }

        return result;
    }

    public static bool IsRunning(GameInfo game, bool separate)
    {
        var list = RunningProcesses(game, separate);
        bool running = list.Count > 0;
        list.ForEach(p => p.Dispose());
        return running;
    }

    /// <summary>
    /// Closes the browser like clicking the window's X (WM_CLOSE to each browser window, repeated until all
    /// windows are gone) – never a forced kill. Returns true when no browser process is left.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> CloseGracefullyAsync(GameInfo game, bool separate, int timeoutSeconds = 12)
    {
        for (int i = 0; i < timeoutSeconds * 2; i++)
        {
            var running = RunningProcesses(game, separate);
            if (running.Count == 0)
            {
                ActionLog.Success($"Performance Launch: closed {game.Publisher} (window close, confirmed by the user)", "running", "closed");
                return true;
            }

            foreach (var p in running)
            {
                using (p)
                {
                    try
                    {
                        p.Refresh();
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            p.CloseMainWindow();
                        }
                    }
                    catch
                    {
                        // ended meanwhile
                    }
                }
            }

            await System.Threading.Tasks.Task.Delay(500);
        }

        return !IsRunning(game, separate);
    }

    public static List<string> BuildArguments(GameInfo game, BrowserLaunchOptions options, bool freshStart)
    {
        var args = new List<string>();
        if (options.SeparateProfile)
        {
            args.Add($"--user-data-dir=\"{ProfileDirectory(game)}\"");
            args.Add("--no-first-run");
            args.Add("--no-default-browser-check");
        }

        if (freshStart)
        {
            // Start flags only work when the browser process starts – they are ignored by an already running browser.
            args.Add("--ignore-gpu-blocklist");
            args.Add("--enable-gpu-rasterization");
            args.Add("--enable-zero-copy");
            args.Add("--force_high_performance_gpu");
            args.Add("--disable-renderer-backgrounding");
            if (options.UnlockFps)
            {
                args.Add("--disable-gpu-vsync");
                args.Add("--disable-frame-rate-limit");
            }

            if (!options.SeparateProfile)
            {
                args.Add("--restore-last-session");
            }

            int scale = Math.Clamp(options.RenderScalePercent, 40, 100);
            if (scale >= 100)
            {
                args.Add("--start-maximized");
            }
            else
            {
                var area = System.Windows.SystemParameters.WorkArea;
                int w = (int)(area.Width * scale / 100.0);
                int h = (int)(area.Height * scale / 100.0);
                args.Add($"--window-size={w},{h}");
                args.Add($"--window-position={(int)(area.Left + (area.Width - w) / 2)},{(int)(area.Top + (area.Height - h) / 2)}");
            }
        }

        args.Add("--new-window");
        args.Add($"\"{options.Url}\"");
        return args;
    }

    /// <param name="freshStart">true when the browser (profile) is not running, so the start flags take effect.</param>
    public static OperationResult Launch(GameInfo game, BrowserLaunchOptions options, bool freshStart)
    {
        if (!IsSupported(game) || game.MainExecutable is null || !File.Exists(game.MainExecutable))
        {
            return OperationResult.Unavailable("This optimization is unavailable for this browser (Performance Launch needs Chrome, Edge, Brave or Opera).");
        }

        if (!TryNormalizeUrl(options.Url, out string url))
        {
            return OperationResult.Fail("Please enter a valid web address (https://…).");
        }

        try
        {
            if (options.SeparateProfile)
            {
                Directory.CreateDirectory(ProfileDirectory(game));
            }

            var args = BuildArguments(game, options with { Url = url }, freshStart);
            var psi = new ProcessStartInfo(game.MainExecutable, string.Join(" ", args)) { UseShellExecute = false };
            using var _ = Process.Start(psi);
            ActionLog.Success($"Performance Launch: {game.Name}", null,
                $"{url} · {(options.SeparateProfile ? "separate profile" : "normal profile")} · {(freshStart ? "GPU start flags active" : "browser was running – flags not applied")}");
            return freshStart
                ? OperationResult.Ok(options.SeparateProfile
                    ? "Started in the separate ConstiBoost window with GPU flags."
                    : "Started in your normal browser (extensions/ad blocker active) with GPU flags. Your previous tabs were restored.")
                : OperationResult.Ok("Opened in your running browser. GPU start flags only apply after a browser restart.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("Could not start the browser: " + Safe.Describe(ex));
        }
    }

    /// <summary>Browser-internal pages with permanent GPU switches (must be opened by the user – browsers block external links to them).</summary>
    public static IReadOnlyList<(string Url, string What)> GpuFlagPages(GameInfo game)
    {
        string scheme = game.Id switch
        {
            "browser:edge" => "edge",
            "browser:brave" => "brave",
            "browser:opera" or "browser:operagx" => "opera",
            _ => "chrome",
        };
        return new List<(string, string)>
        {
            ($"{scheme}://flags/#ignore-gpu-blocklist", "'Override software rendering list' → Enabled (most important if WebGL runs on the CPU)"),
            ($"{scheme}://flags/#enable-gpu-rasterization", "'GPU rasterization' → Enabled"),
            ($"{scheme}://flags/#use-angle", "'Choose ANGLE graphics backend' → try D3D11, then OpenGL – keep the faster one"),
        };
    }
}
