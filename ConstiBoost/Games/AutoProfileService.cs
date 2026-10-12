using ConstiBoost.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.Games;

/// <summary>Saved per-game Game Focus options that are applied automatically when the game starts.</summary>
public sealed class GameAutoProfile
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public List<string> ProcessRoots { get; set; } = new();
    public string? MainExecutable { get; set; }
    public string? EpicAppName { get; set; }
    public bool Enabled { get; set; }
    public GamingProfile Profile { get; set; } = GamingProfile.MaxFps;
    public bool BoostPriority { get; set; } = true;
    public bool GpuPreference { get; set; }
    public bool MuteNotifications { get; set; } = true;
    public bool LowerPriority { get; set; } = true;
    public bool CapFps { get; set; }
    public int FpsCap { get; set; } = 60;
    public List<string> CloseApps { get; set; } = new();
}

/// <summary>
/// Watches (every 5 s, only while at least one auto profile is enabled) for games with an auto profile.
/// Game starts → Game Focus starts automatically. Game closed for 20 s → Focus stops and everything is restored.
/// </summary>
public sealed class AutoProfileService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private Dictionary<string, GameAutoProfile> _profiles = new();
    private string? _autoStartedGameId;
    private DateTime? _goneSince;
    private bool _busy;

    public static AutoProfileService Instance { get; } = new();

    private static string FilePath => Path.Combine(AppPaths.Root, "auto-profiles.json");

    private AutoProfileService()
    {
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                _profiles = (JsonSerializer.Deserialize<List<GameAutoProfile>>(File.ReadAllText(FilePath)) ?? new())
                    .ToDictionary(p => p.GameId);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Auto profiles unreadable: " + ex.Message);
        }

        UpdateTimer();
    }

    public GameAutoProfile? Get(string gameId) => _profiles.TryGetValue(gameId, out var p) ? p : null;

    public IReadOnlyCollection<GameAutoProfile> All => _profiles.Values;

    public void Save(GameAutoProfile profile)
    {
        _profiles[profile.GameId] = profile;
        Safe.Try(() => File.WriteAllText(FilePath, JsonSerializer.Serialize(_profiles.Values.ToList(), new JsonSerializerOptions { WriteIndented = true })), "Save auto profiles");
        ActionLog.Success($"Auto profile: {profile.GameName}", null, profile.Enabled ? $"ON ({GamingModeService.ProfileName(profile.Profile)})" : "OFF");
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (_profiles.Values.Any(p => p.Enabled))
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async Task TickAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var focus = GameFocusService.Instance;
            if (_autoStartedGameId != null)
            {
                if (!focus.IsActive || focus.FocusedGameId != _autoStartedGameId)
                {
                    _autoStartedGameId = null; // user stopped or changed it manually
                    return;
                }

                var profile = Get(_autoStartedGameId);
                bool running = profile != null && await Task.Run(() => IsRunning(profile));
                _goneSince = running ? null : _goneSince ?? DateTime.UtcNow;
                if (_goneSince.HasValue && DateTime.UtcNow - _goneSince.Value > TimeSpan.FromSeconds(20))
                {
                    var messages = await focus.StopAsync();
                    Notify(L.F("Game closed – Auto-Focus for {0} stopped and everything restored.", profile?.GameName) + (messages.Count > 0 ? L.T(" Notes: ") + string.Join("; ", messages) : string.Empty));
                    _autoStartedGameId = null;
                    _goneSince = null;
                }

                return;
            }

            if (focus.IsActive || GamingModeService.Instance.IsActive)
            {
                return; // never interfere with a manual session
            }

            foreach (var profile in _profiles.Values.Where(p => p.Enabled))
            {
                if (!await Task.Run(() => IsRunning(profile)))
                {
                    continue;
                }

                var candidates = await Task.Run(BackgroundAppsService.GetCandidates);
                var apps = candidates.Where(c => profile.CloseApps.Contains(c.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
                var game = new GameInfo
                {
                    Id = profile.GameId,
                    Name = profile.GameName,
                    InstallPath = profile.InstallPath,
                    MainExecutable = profile.MainExecutable,
                    EpicAppName = profile.EpicAppName,
                    ExtraProcessRoots = profile.ProcessRoots.Where(r => !string.Equals(r, profile.InstallPath, StringComparison.OrdinalIgnoreCase)).ToList(),
                };
                var messages = await focus.StartAsync(game, new FocusOptions(profile.Profile, apps, profile.LowerPriority, profile.BoostPriority,
                    profile.GpuPreference, profile.MuteNotifications, profile.CapFps ? profile.FpsCap : null,
                    DisableThrottling: Services.SettingsService.Instance.Current.FocusDisableThrottling));
                _autoStartedGameId = profile.GameId;
                _goneSince = null;
                Notify(L.F("🎯 {0} started – Auto-Focus ({1}) is active.", profile.GameName, GamingModeService.ProfileName(profile.Profile)) + (messages.Count > 0 ? L.T(" Notes: ") + string.Join("; ", messages) : string.Empty));
                break;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Auto profile tick failed", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool IsRunning(GameAutoProfile profile)
    {
        var processes = GameFocusService.FindGameProcesses(profile.ProcessRoots.Count > 0 ? profile.ProcessRoots : new List<string> { profile.InstallPath });
        bool any = processes.Count > 0;
        foreach (var p in processes)
        {
            p.Dispose();
        }

        return any;
    }

    private static void Notify(string message)
    {
        NotificationService.Instance.Show(message, NotificationKind.Info);
        TrayService.Instance.ShowBalloon("ConstiBoost", message);
    }
}
