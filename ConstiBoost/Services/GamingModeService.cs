using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Fortnite;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public sealed class GamingModeState
{
    public DateTime ActivatedAt { get; set; }
    public RegistryValueSnapshot? GameMode { get; set; }
    public string? PreviousScheme { get; set; }
    public string? PreviousOverlay { get; set; }
    public List<ClosedApp> ClosedApps { get; set; } = new();
    public List<PriorityChange> PriorityChanges { get; set; } = new();
    public GamingProfile Profile { get; set; } = GamingProfile.MaxFps;
    public string? ProcessorMaxScheme { get; set; }
    public uint? PreviousProcessorMaxAc { get; set; }
    public uint? PreviousProcessorMaxDc { get; set; }
    public string? PreviousFortniteFrameLimit { get; set; }
}

public sealed record GamingModeOptions(GamingProfile Profile, bool LowerBackgroundPriority, int? FortniteFpsCap, IReadOnlyCollection<string>? KeepProcesses = null);

public sealed class ClosedApp
{
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
}

public sealed class PriorityChange
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string OriginalPriority { get; set; } = nameof(ProcessPriorityClass.Normal);
}

/// <summary>
/// Temporary gaming session: Game Mode on, performance power mode, selected apps closed and known
/// background apps set to below-normal priority. Deactivating restores everything that can be restored.
/// The state is saved to disk so it can be restored even after a crash.
/// </summary>
public sealed class GamingModeService : ObservableObject
{
    private GamingModeState? _state;

    public static GamingModeService Instance { get; } = new();

    public bool IsActive => _state != null;

    public DateTime? ActivatedAt => _state?.ActivatedAt;

    public GamingProfile? ActiveProfile => _state?.Profile;

    public static string ProfileName(GamingProfile profile) => profile switch
    {
        GamingProfile.MaxFps => "MAX FPS",
        GamingProfile.Balanced => "BALANCED",
        _ => "COOL",
    };

    public event Action? Changed;

    public void LoadPersistedState()
    {
        try
        {
            if (File.Exists(AppPaths.GamingModeStateFile))
            {
                _state = JsonSerializer.Deserialize<GamingModeState>(File.ReadAllText(AppPaths.GamingModeStateFile));
                RaiseChanged();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Gaming mode state unreadable: " + ex.Message);
        }
    }

    public Task<List<string>> ActivateAsync(IEnumerable<AppGroup> appsToClose, bool lowerBackgroundPriority) =>
        ActivateAsync(appsToClose, new GamingModeOptions(GamingProfile.MaxFps, lowerBackgroundPriority, null));

    public async Task<List<string>> ActivateAsync(IEnumerable<AppGroup> appsToClose, GamingModeOptions options)
    {
        bool lowerBackgroundPriority = options.LowerBackgroundPriority;
        var messages = new List<string>();
        if (_state != null)
        {
            return messages;
        }

        var state = new GamingModeState { ActivatedAt = DateTime.Now, Profile = options.Profile };
        _state = state;
        Persist();

        // 1. Game Mode
        try
        {
            state.GameMode = RegistryValueSnapshot.Capture("HKCU", GameFeatures.GameBarKey, GameFeatures.GameModeValue);
            bool before = GameFeatures.GameModeEnabled;
            RegistryHelper.WriteDword("HKCU", GameFeatures.GameBarKey, GameFeatures.GameModeValue, 1);
            ActionLog.Success("Gaming Mode: Game Mode", before ? "ON" : "OFF", "ON");
        }
        catch (Exception ex)
        {
            messages.Add("Game Mode: " + Safe.Describe(ex));
            ActionLog.Failed("Gaming Mode: Game Mode", null, "ON", Safe.Describe(ex));
        }

        Persist();

        // 2. Power – depends on the profile.
        //    MAX FPS:  Best performance power mode (highest clocks, most heat)
        //    BALANCED: Balanced power mode (CPU still boosts, less sustained heat)
        //    COOL:     Balanced + maximum processor state 99 % (disables CPU boost; Windows keeps all thermal protection)
        try
        {
            var overlay = PowerManager.GetOverlay();
            var scheme = PowerManager.GetActiveSchemeId();
            Guid targetOverlay = options.Profile == GamingProfile.MaxFps ? PowerManager.OverlayBestPerformance : PowerManager.OverlayBalanced;
            if (overlay != null && scheme == PowerManager.Balanced)
            {
                if (overlay != targetOverlay)
                {
                    state.PreviousOverlay = overlay.Value.ToString();
                    PowerManager.SetOverlay(targetOverlay);
                    ActionLog.Success("Gaming Mode: Power mode", PowerManager.OverlayName(overlay), PowerManager.OverlayName(targetOverlay));
                }
            }
            else if (options.Profile == GamingProfile.MaxFps && scheme != null && scheme != PowerManager.HighPerformance && scheme != PowerManager.UltimatePerformance &&
                     PowerManager.GetSchemes().Any(s => s.Id == PowerManager.HighPerformance))
            {
                state.PreviousScheme = scheme.Value.ToString();
                PowerManager.SetActiveScheme(PowerManager.HighPerformance);
                ActionLog.Success("Gaming Mode: Power plan", PowerManager.GetFriendlyName(scheme.Value), "High performance");
            }
            else if (options.Profile != GamingProfile.MaxFps && (scheme == PowerManager.HighPerformance || scheme == PowerManager.UltimatePerformance) &&
                     PowerManager.GetSchemes().Any(s => s.Id == PowerManager.Balanced))
            {
                state.PreviousScheme = scheme.Value.ToString();
                PowerManager.SetActiveScheme(PowerManager.Balanced);
                ActionLog.Success("Gaming Mode: Power plan", PowerManager.GetFriendlyName(scheme.Value), "Balanced");
            }
        }
        catch (Exception ex)
        {
            messages.Add("Power: " + Safe.Describe(ex));
            ActionLog.Failed("Gaming Mode: Power", null, ProfileName(options.Profile), Safe.Describe(ex));
        }

        if (options.Profile == GamingProfile.Cool && PowerManager.GetActiveSchemeId() is Guid active && PowerManager.GetProcessorMaxState(active) is { } max)
        {
            state.ProcessorMaxScheme = active.ToString();
            state.PreviousProcessorMaxAc = max.ac;
            state.PreviousProcessorMaxDc = max.dc;
            Persist();
            var r = await PowerTweakService.SetProcessorMaxStateAsync(active, Math.Min(max.ac, 99u), Math.Min(max.dc, 99u), "Gaming Mode (COOL): maximum processor state");
            if (!r.Success)
            {
                state.ProcessorMaxScheme = null;
                messages.Add("CPU boost limit: " + (r.TechnicalDetails ?? r.Message));
            }
        }

        if (options.FortniteFpsCap is int cap)
        {
            var (result, previous) = FortniteConfigService.SetFrameRateLimit(cap.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture), null);
            if (result.Success)
            {
                state.PreviousFortniteFrameLimit = previous;
            }
            else
            {
                messages.Add(result.Message);
            }
        }

        Persist();

        // 3. Close selected apps
        foreach (var group in appsToClose)
        {
            var (closed, message) = await BackgroundAppsService.CloseGroupAsync(group);
            if (closed)
            {
                state.ClosedApps.Add(new ClosedApp { Name = group.DisplayName, Path = group.Path });
            }
            else
            {
                messages.Add(message);
            }
        }

        Persist();

        // 4. Lower priority of remaining background apps
        if (lowerBackgroundPriority)
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (options.KeepProcesses?.Contains(name, StringComparer.OrdinalIgnoreCase) == true)
                        {
                            continue; // the game itself (e.g. the browser running a web game)
                        }

                        if ((!BackgroundAppsService.KnownBackgroundApps.Contains(name) && !BackgroundAppsService.BrowserProcesses.Contains(name)) ||
                            BackgroundAppsService.KeepPriority.Contains(name))
                        {
                            continue;
                        }

                        if (!ProcessSafety.CanTouch(process, out _) || process.PriorityClass != ProcessPriorityClass.Normal)
                        {
                            continue;
                        }

                        state.PriorityChanges.Add(new PriorityChange
                        {
                            Pid = process.Id,
                            Name = name,
                            StartTime = process.StartTime,
                            OriginalPriority = process.PriorityClass.ToString(),
                        });
                        process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    catch
                    {
                        // Access denied for this process – skipped.
                    }
                }
            }

            if (state.PriorityChanges.Count > 0)
            {
                ActionLog.Success($"Gaming Mode: lowered priority of {state.PriorityChanges.Count} background process(es)", "Normal", "Below normal");
            }
        }

        Persist();
        ActionLog.Success("Gaming Mode", "OFF", "ACTIVE");
        RaiseChanged();
        return messages;
    }

    public async Task<List<string>> DeactivateAsync(bool relaunchClosedApps)
    {
        var messages = new List<string>();
        var state = _state;
        if (state is null)
        {
            return messages;
        }

        try
        {
            state.GameMode?.Restore();
            if (state.GameMode != null)
            {
                ActionLog.Success("Gaming Mode: restore Game Mode", "ON", state.GameMode.DisplayValue);
            }
        }
        catch (Exception ex)
        {
            messages.Add("Game Mode: " + Safe.Describe(ex));
        }

        try
        {
            if (state.PreviousOverlay != null)
            {
                var guid = Guid.Parse(state.PreviousOverlay);
                PowerManager.SetOverlay(guid);
                ActionLog.Success("Gaming Mode: restore power mode", "Best performance", PowerManager.OverlayName(guid));
            }

            if (state.PreviousScheme != null)
            {
                var guid = Guid.Parse(state.PreviousScheme);
                PowerManager.SetActiveScheme(guid);
                ActionLog.Success("Gaming Mode: restore power plan", "High performance", PowerManager.GetFriendlyName(guid));
            }
        }
        catch (Exception ex)
        {
            messages.Add("Power: " + Safe.Describe(ex));
            ActionLog.Failed("Gaming Mode: restore power", null, null, Safe.Describe(ex));
        }

        if (state.ProcessorMaxScheme != null && state.PreviousProcessorMaxAc is uint ac && state.PreviousProcessorMaxDc is uint dc)
        {
            var r = await PowerTweakService.SetProcessorMaxStateAsync(Guid.Parse(state.ProcessorMaxScheme), ac, dc, "Gaming Mode: restore maximum processor state");
            if (!r.Success)
            {
                messages.Add("CPU boost limit: " + (r.TechnicalDetails ?? r.Message));
            }
        }

        if (state.PreviousFortniteFrameLimit != null)
        {
            var (result, _) = FortniteConfigService.SetFrameRateLimit(state.PreviousFortniteFrameLimit, null);
            if (!result.Success)
            {
                messages.Add(result.Message);
            }
        }

        int restoredPriorities = 0;
        foreach (var change in state.PriorityChanges)
        {
            try
            {
                using var process = Process.GetProcessById(change.Pid);
                if (process.ProcessName == change.Name && Math.Abs((process.StartTime - change.StartTime).TotalSeconds) < 1)
                {
                    process.PriorityClass = Enum.Parse<ProcessPriorityClass>(change.OriginalPriority);
                    restoredPriorities++;
                }
            }
            catch
            {
                // Process already exited – nothing to restore.
            }
        }

        if (restoredPriorities > 0)
        {
            ActionLog.Success($"Gaming Mode: restored priority of {restoredPriorities} process(es)", "Below normal", "Normal");
        }

        if (relaunchClosedApps)
        {
            foreach (var app in state.ClosedApps.Where(a => !string.IsNullOrEmpty(a.Path) && File.Exists(a.Path)))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(app.Path!) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(app.Path!) ?? string.Empty });
                    ActionLog.Success("Gaming Mode: reopen " + app.Name, "Closed", "Started");
                }
                catch (Exception ex)
                {
                    messages.Add($"{app.Name}: {Safe.Describe(ex)}");
                }
            }
        }

        _state = null;
        Safe.Try(() => File.Delete(AppPaths.GamingModeStateFile));
        ActionLog.Success("Gaming Mode", "ACTIVE", "OFF");
        RaiseChanged();
        return messages;
    }

    private void Persist()
    {
        try
        {
            File.WriteAllText(AppPaths.GamingModeStateFile, JsonSerializer.Serialize(_state));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Gaming mode state could not be saved: " + ex.Message);
        }
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActivatedAt));
        OnPropertyChanged(nameof(ActiveProfile));
        Changed?.Invoke();
    }
}
