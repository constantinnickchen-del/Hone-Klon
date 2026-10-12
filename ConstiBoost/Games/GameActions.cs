using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ConstiBoost.Core;
using ConstiBoost.Logging;

namespace ConstiBoost.Games;

/// <summary>Launch / uninstall / open folder – always through the official launcher or the game's own uninstaller.</summary>
public static class GameActions
{
    public static OperationResult Launch(GameInfo game)
    {
        try
        {
            if (game.LaunchUri != null || game.AppUserModelId != null)
            {
                var start = game.AppUserModelId != null
                    ? new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{game.AppUserModelId}") { UseShellExecute = true }
                    : new ProcessStartInfo(game.LaunchUri!) { UseShellExecute = true };
                Process.Start(start);
                ActionLog.Success("Launch game: " + game.Name, null, game.SourceText);
                return OperationResult.Ok($"{game.Name} is starting…");
            }

            string target = game.Source switch
            {
                GameSource.Steam => $"steam://rungameid/{game.SteamAppId}",
                GameSource.Epic => $"com.epicgames.launcher://apps/{Uri.EscapeDataString(game.EpicLaunchId ?? game.EpicAppName ?? game.Name)}?action=launch&silent=true",
                _ => game.MainExecutable ?? throw new InvalidOperationException("No executable found."),
            };
            var psi = new ProcessStartInfo(target) { UseShellExecute = true };
            if (game.Source == GameSource.Installed && game.MainExecutable != null)
            {
                psi.WorkingDirectory = Path.GetDirectoryName(game.MainExecutable) ?? string.Empty;
            }

            Process.Start(psi);
            ActionLog.Success("Launch game: " + game.Name, null, game.SourceText);
            return OperationResult.Ok($"{game.Name} is starting…");
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Launch game: " + game.Name, null, null, Safe.Describe(ex));
            return OperationResult.Fail($"{game.Name} couldn't be started: {Safe.Describe(ex)}");
        }
    }

    /// <summary>
    /// Starts the official uninstaller. ConstiBoost never deletes game files itself.
    /// </summary>
    public static OperationResult Uninstall(GameInfo game)
    {
        try
        {
            if (game.PackageFullName != null)
            {
                if (!Regex.IsMatch(game.PackageFullName, @"^[A-Za-z0-9._~\-]+$"))
                {
                    return OperationResult.Fail("Unexpected package name – uninstall it via Windows Settings > Apps.");
                }

                // Documented PowerShell cmdlet, current user only, no admin rights.
                Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Remove-AppxPackage -Package '{game.PackageFullName}'\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                ActionLog.Success("Uninstall game: " + game.Name, "Installed", "Remove-AppxPackage started");
                return OperationResult.Ok($"{game.Name} is being removed. This can take a moment – press RESCAN afterwards.");
            }

            if (game.Source == GameSource.Minecraft)
            {
                Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true });
                return OperationResult.Ok("Windows Settings > Apps opens – uninstall 'Minecraft Launcher' there. Your worlds in %APPDATA%\\.minecraft are kept.");
            }

            switch (game.Source)
            {
                case GameSource.Xbox:
                    Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true });
                    return OperationResult.Ok("Windows Settings > Apps opens – uninstall the game there (or in the Xbox app).");
                case GameSource.Steam:
                    Process.Start(new ProcessStartInfo($"steam://uninstall/{game.SteamAppId}") { UseShellExecute = true });
                    break;
                case GameSource.Epic:
                    Process.Start(new ProcessStartInfo("com.epicgames.launcher://store/library") { UseShellExecute = true });
                    ActionLog.Success("Uninstall game: " + game.Name, null, "Epic Games Launcher opened");
                    return OperationResult.Ok("The Epic Games Launcher opens – in your Library click ··· on the game and choose 'Uninstall'.");
                default:
                    if (string.IsNullOrWhiteSpace(game.UninstallString))
                    {
                        return OperationResult.Fail("This game has no registered uninstaller. Remove it via Windows Settings > Apps.");
                    }

                    var (exe, args) = Split(game.UninstallString);
                    Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    break;
            }

            ActionLog.Success("Uninstall game: " + game.Name, "Installed", "Uninstaller started");
            return OperationResult.Ok($"The uninstaller for {game.Name} was started. Follow its instructions.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return OperationResult.Fail("Uninstall cancelled (administrator permission declined).");
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Uninstall game: " + game.Name, null, null, Safe.Describe(ex));
            return OperationResult.Fail($"The uninstaller couldn't be started: {Safe.Describe(ex)}");
        }
    }

    public static void OpenFolder(GameInfo game) =>
        Safe.Try(() => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{game.InstallPath}\"") { UseShellExecute = true }), "Open game folder");

    public static (string exe, string args) Split(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end > 0)
            {
                return (command[1..end], command[(end + 1)..].Trim());
            }
        }

        var match = Regex.Match(command, @"^(.+?\.exe)\s*(.*)$", RegexOptions.IgnoreCase);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (command, string.Empty);
    }
}
