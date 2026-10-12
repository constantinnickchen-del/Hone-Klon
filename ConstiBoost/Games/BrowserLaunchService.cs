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

public sealed record BrowserLaunchOptions(string Url, int RenderScalePercent, bool UnlockFps);

/// <summary>
/// ⚡ Performance Launch for web games: starts a Chromium browser (Chrome, Edge, Brave, Opera) with a separate,
/// clean ConstiBoost profile (no extensions, your normal profile and data stay untouched) and GPU-friendly
/// start flags, in an app window whose size = render scale – fewer pixels = more FPS for every WebGL game.
/// Nothing inside the website is read or modified.
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

    public static List<string> BuildArguments(GameInfo game, BrowserLaunchOptions options)
    {
        var args = new List<string>
        {
            $"--user-data-dir=\"{ProfileDirectory(game)}\"",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-background-networking",
            "--ignore-gpu-blocklist",
            "--enable-gpu-rasterization",
            "--enable-zero-copy",
            "--force_high_performance_gpu",
        };

        if (options.UnlockFps)
        {
            args.Add("--disable-gpu-vsync");
            args.Add("--disable-frame-rate-limit");
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
            int x = (int)(area.Left + (area.Width - w) / 2);
            int y = (int)(area.Top + (area.Height - h) / 2);
            args.Add($"--window-size={w},{h}");
            args.Add($"--window-position={x},{y}");
        }

        args.Add($"--app=\"{options.Url}\"");
        return args;
    }

    public static OperationResult Launch(GameInfo game, BrowserLaunchOptions options)
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
            Directory.CreateDirectory(ProfileDirectory(game));
            var args = BuildArguments(game, options with { Url = url });
            var psi = new ProcessStartInfo(game.MainExecutable, string.Join(" ", args)) { UseShellExecute = false };
            using var _ = Process.Start(psi);
            ActionLog.Success($"Performance Launch: {game.Name}", null, $"{url} · render size {options.RenderScalePercent} %{(options.UnlockFps ? " · FPS unlocked" : string.Empty)}");
            return OperationResult.Ok("Started. Tip: log in once in this window – the ConstiBoost browser profile remembers it. Your normal browser profile is not touched.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("Could not start the browser: " + Safe.Describe(ex));
        }
    }
}
