using ConstiBoost.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConstiBoost.Games;

/// <summary>
/// Installed browsers as "Browser games" entries, so web games (WebGL) get Game Focus, real FPS measurement and tips.
/// Only the browser executable location is checked – no browser profile, history or cookie data is read.
/// </summary>
public static class BrowserGamesScanner
{
    private static readonly (string id, string name, string process, string[] candidates)[] Browsers =
    {
        ("chrome", "Google Chrome", "chrome", new[] { @"%ProgramFiles%\Google\Chrome\Application\chrome.exe", @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe", @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe" }),
        ("edge", "Microsoft Edge", "msedge", new[] { @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe", @"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" }),
        ("brave", "Brave", "brave", new[] { @"%ProgramFiles%\BraveSoftware\Brave-Browser\Application\brave.exe", @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\Application\brave.exe" }),
        ("operagx", "Opera GX", "opera", new[] { @"%LOCALAPPDATA%\Programs\Opera GX\opera.exe" }),
        ("opera", "Opera", "opera", new[] { @"%LOCALAPPDATA%\Programs\Opera\opera.exe" }),
        ("firefox", "Firefox", "firefox", new[] { @"%ProgramFiles%\Mozilla Firefox\firefox.exe", @"%ProgramFiles(x86)%\Mozilla Firefox\firefox.exe" }),
    };

    public static IEnumerable<GameInfo> Scan()
    {
        foreach (var (id, name, process, candidates) in Browsers)
        {
            string? exe = candidates.Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);
            if (exe is null)
            {
                continue;
            }

            yield return new GameInfo
            {
                Id = "browser:" + id,
                Name = L.F("Browser games · {0}", name),
                Source = GameSource.Browser,
                Publisher = name,
                InstallPath = Path.GetDirectoryName(exe)!,
                MainExecutable = exe,
                ExecutableNames = new List<string> { process },
                LaunchUri = exe,
            };
        }
    }

    /// <summary>Settings page that contains the hardware acceleration switch (has to be opened by the user – browsers block external links to it).</summary>
    public static string AccelerationSettingsUrl(GameInfo game) => game.Id switch
    {
        "browser:edge" => "edge://settings/system",
        "browser:brave" => "brave://settings/system",
        "browser:firefox" => "about:preferences  (General → Performance → 'Use hardware acceleration when available')",
        "browser:opera" or "browser:operagx" => "opera://settings/system",
        _ => "chrome://settings/system",
    };

    public static string GpuInfoUrl(GameInfo game) => game.Id switch
    {
        "browser:edge" => "edge://gpu",
        "browser:firefox" => "about:support",
        "browser:brave" => "brave://gpu",
        "browser:opera" or "browser:operagx" => "opera://gpu",
        _ => "chrome://gpu",
    };
}
