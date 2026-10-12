using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.WinSys;

namespace ConstiBoost.Games;

public sealed record FocusOptions(
    GamingProfile Profile,
    IReadOnlyList<AppGroup> AppsToClose,
    bool LowerBackgroundPriority,
    bool BoostGamePriority,
    bool HighPerformanceGpu,
    bool MuteNotifications,
    int? FortniteFpsCap,
    bool BoostBrowserTab = false);

public sealed class FocusState
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public List<string> ProcessRoots { get; set; } = new();
    public bool BoostGamePriority { get; set; }
    public bool BoostBrowserTab { get; set; }
    public RegistryValueSnapshot? GpuPreference { get; set; }
    public RegistryValueSnapshot? Notifications { get; set; }
    public DateTime Started { get; set; }
}

/// <summary>
/// 🎯 Game Focus: puts every safe, reversible system resource behind one game:
/// Gaming Mode (profile, Game Mode, power, closed apps, lower background priority),
/// Windows per-app "High performance" GPU preference, muted toast notifications and
/// Above-normal priority for the game process while it runs. Stop restores everything.
/// </summary>
public sealed class GameFocusService : ObservableObject
{
    private const string GpuPreferenceKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string NotificationsKey = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
    private const string NotificationsValue = "NOC_GLOBAL_SETTING_TOASTS_ENABLED";

    private readonly DispatcherTimer _watcher = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly HashSet<int> _boosted = new();
    private readonly Dictionary<int, TimeSpan> _cpuTimes = new();
    private int? _boostedRenderer;
    private FocusState? _state;

    public static GameFocusService Instance { get; } = new();

    private static string StateFile => Path.Combine(AppPaths.Root, "focus-state.json");

    private GameFocusService()
    {
        _watcher.Tick += (_, _) => Watch();
    }

    public FocusState? State => _state;

    public bool IsActive => _state != null;

    public string? FocusedGameId => _state?.GameId;

    public event Action? Changed;

    public void LoadPersisted()
    {
        try
        {
            if (File.Exists(StateFile))
            {
                _state = JsonSerializer.Deserialize<FocusState>(File.ReadAllText(StateFile));
                if (_state != null)
                {
                    _watcher.Start();
                    Raise();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Focus state unreadable: " + ex.Message);
        }
    }

    public async Task<List<string>> StartAsync(GameInfo game, FocusOptions options)
    {
        var messages = new List<string>();
        if (_state != null)
        {
            messages.AddRange(await StopAsync(false));
        }

        var state = new FocusState { GameId = game.Id, GameName = game.Name, InstallPath = game.InstallPath, ProcessRoots = game.ProcessRoots.ToList(), BoostGamePriority = options.BoostGamePriority, BoostBrowserTab = options.BoostBrowserTab && game.IsBrowser, Started = DateTime.Now };
        _state = state;
        Persist();

        if (options.HighPerformanceGpu && game.MainExecutable != null)
        {
            try
            {
                state.GpuPreference = RegistryValueSnapshot.Capture("HKCU", GpuPreferenceKey, game.MainExecutable);
                using var root = RegistryHelper.OpenBase("HKCU");
                using var key = root.CreateSubKey(GpuPreferenceKey, true);
                key.SetValue(game.MainExecutable, "GpuPreference=2;", Microsoft.Win32.RegistryValueKind.String);
                ActionLog.Success($"Game Focus: GPU preference for {game.Name}", state.GpuPreference.DisplayValue, "High performance");
            }
            catch (Exception ex)
            {
                messages.Add("GPU preference: " + Safe.Describe(ex));
            }
        }

        if (options.MuteNotifications)
        {
            try
            {
                state.Notifications = RegistryValueSnapshot.Capture("HKCU", NotificationsKey, NotificationsValue);
                RegistryHelper.WriteDword("HKCU", NotificationsKey, NotificationsValue, 0);
                ActionLog.Success("Game Focus: notification banners", state.Notifications.DisplayValue, "Muted");
            }
            catch (Exception ex)
            {
                messages.Add("Notifications: " + Safe.Describe(ex));
            }
        }

        Persist();

        if (!GamingModeService.Instance.IsActive)
        {
            messages.AddRange(await GamingModeService.Instance.ActivateAsync(options.AppsToClose,
                new GamingModeOptions(options.Profile, options.LowerBackgroundPriority, game.IsFortnite ? options.FortniteFpsCap : null, game.ExecutableNames)));
        }

        game.IsFocused = true;
        _watcher.Start();
        Watch();
        ActionLog.Success("Game Focus", "OFF", game.Name);
        Raise();
        return messages;
    }

    public async Task<List<string>> StopAsync(bool stopGamingMode = true)
    {
        var messages = new List<string>();
        var state = _state;
        if (state is null)
        {
            return messages;
        }

        _watcher.Stop();
        foreach (int pid in _boosted.ToList())
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.PriorityClass == ProcessPriorityClass.AboveNormal)
                {
                    p.PriorityClass = ProcessPriorityClass.Normal;
                }
            }
            catch
            {
                // process ended – Windows reset it already
            }
        }

        _boosted.Clear();
        _cpuTimes.Clear();
        _boostedRenderer = null;
        foreach (var snapshot in new[] { state.GpuPreference, state.Notifications })
        {
            if (snapshot is null)
            {
                continue;
            }

            try
            {
                snapshot.Restore();
                ActionLog.Success("Game Focus: restore " + snapshot.ValueName, null, snapshot.DisplayValue);
            }
            catch (Exception ex)
            {
                messages.Add($"{snapshot.ValueName}: {Safe.Describe(ex)}");
            }
        }

        if (stopGamingMode && GamingModeService.Instance.IsActive)
        {
            messages.AddRange(await GamingModeService.Instance.DeactivateAsync(SettingsService.Instance.Current.GamingModeRelaunchClosedApps));
        }

        _state = null;
        Safe.Try(() => File.Delete(StateFile));
        ActionLog.Success("Game Focus", state.GameName, "OFF");
        Raise();
        return messages;
    }

    /// <summary>Returns the PIDs of processes running from the game folder.</summary>
    public static List<Process> FindGameProcesses(IEnumerable<string> processRoots)
    {
        var roots = processRoots.Select(r => GameLibraryService.NormalizePath(r) + "\\").ToList();
        var found = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            string? path = ProcessSafety.TryGetPath(p.Id);
            if (path != null && roots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(p);
            }
            else
            {
                p.Dispose();
            }
        }

        return found;
    }

    private void Watch()
    {
        var state = _state;
        if (state is null)
        {
            return;
        }

        try
        {
            var processes = FindGameProcesses(state.ProcessRoots.Count > 0 ? state.ProcessRoots : new List<string> { state.InstallPath });
            if (processes.Count > 0)
            {
                LastPlayedService.RecordSession(state.GameId);
            }

            if (state.BoostBrowserTab)
            {
                BoostBrowser(processes);
                return;
            }

            foreach (var p in processes)
            {
                using (p)
                {
                    if (!state.BoostGamePriority || _boosted.Contains(p.Id))
                    {
                        continue;
                    }

                    try
                    {
                        if (p.PriorityClass == ProcessPriorityClass.Normal)
                        {
                            p.PriorityClass = ProcessPriorityClass.AboveNormal;
                            ActionLog.Success($"Game Focus: priority {p.ProcessName}", "Normal", "AboveNormal");
                        }
                    }
                    catch
                    {
                        // Anti-cheat protected – expected, nothing to do.
                    }

                    _boosted.Add(p.Id);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Focus watcher: " + ex.Message);
        }
    }

    /// <summary>
    /// Browser games: instead of raising every browser process (all tabs, extensions…),
    /// only the GPU process and the renderer that currently uses the most CPU (= the game
    /// tab) get Above-normal priority. Everything is reverted on Stop.
    /// </summary>
    private void BoostBrowser(List<Process> processes)
    {
        try
        {
            var commandLines = BrowserProcessInfo.CommandLines(processes.Select(p => p.Id));
            int? gpu = null;
            int? topRenderer = null;
            double topDelta = 0;
            var seen = new HashSet<int>();
            foreach (var p in processes)
            {
                seen.Add(p.Id);
                commandLines.TryGetValue(p.Id, out string? cmd);
                cmd ??= string.Empty;
                if (cmd.Contains("--type=gpu-process", StringComparison.OrdinalIgnoreCase))
                {
                    gpu = p.Id;
                    continue;
                }

                if (!cmd.Contains("--type=renderer", StringComparison.OrdinalIgnoreCase)
                    && !cmd.Contains("-contentproc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TimeSpan cpu;
                try
                {
                    cpu = p.TotalProcessorTime;
                }
                catch
                {
                    continue;
                }

                if (_cpuTimes.TryGetValue(p.Id, out var last))
                {
                    double delta = (cpu - last).TotalMilliseconds;
                    if (delta > topDelta)
                    {
                        topDelta = delta;
                        topRenderer = p.Id;
                    }
                }

                _cpuTimes[p.Id] = cpu;
            }

            foreach (int stale in _cpuTimes.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _cpuTimes.Remove(stale);
            }

            if (gpu is int g)
            {
                Raise(processes.First(p => p.Id == g), "browser GPU process");
            }

            // Only switch the boosted tab when the new one clearly works (>15 % of one core).
            if (topRenderer is int r && topDelta > 450 && r != _boostedRenderer)
            {
                if (_boostedRenderer is int old && _boosted.Remove(old))
                {
                    Lower(old);
                }

                Raise(processes.First(p => p.Id == r), "game tab");
                _boostedRenderer = r;
            }
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }

    private void Raise(Process p, string role)
    {
        if (_boosted.Contains(p.Id))
        {
            return;
        }

        try
        {
            if (p.PriorityClass == ProcessPriorityClass.Normal)
            {
                p.PriorityClass = ProcessPriorityClass.AboveNormal;
                ActionLog.Success($"Game Focus: priority {p.ProcessName} ({role})", "Normal", "AboveNormal");
                _boosted.Add(p.Id);
            }
        }
        catch
        {
            // protected – nothing to do
        }
    }

    private static void Lower(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.PriorityClass == ProcessPriorityClass.AboveNormal)
            {
                p.PriorityClass = ProcessPriorityClass.Normal;
            }
        }
        catch
        {
            // ended
        }
    }

    private void Persist()
    {
        Safe.Try(() => File.WriteAllText(StateFile, JsonSerializer.Serialize(_state)), "Persist focus state");
    }

    private void Raise()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(FocusedGameId));
        Changed?.Invoke();
    }
}
