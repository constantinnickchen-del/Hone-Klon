using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
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
}

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

    public async Task<List<string>> ActivateAsync(IEnumerable<AppGroup> appsToClose, bool lowerBackgroundPriority)
    {
        var messages = new List<string>();
        if (_state != null)
        {
            return messages;
        }

        var state = new GamingModeState { ActivatedAt = DateTime.Now };
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

        // 2. Power
        try
        {
            var overlay = PowerManager.GetOverlay();
            var scheme = PowerManager.GetActiveSchemeId();
            if (overlay != null && scheme == PowerManager.Balanced)
            {
                state.PreviousOverlay = overlay.Value.ToString();
                PowerManager.SetOverlay(PowerManager.OverlayBestPerformance);
                ActionLog.Success("Gaming Mode: Power mode", PowerManager.OverlayName(overlay), "Best performance");
            }
            else if (scheme != null && scheme != PowerManager.HighPerformance && scheme != PowerManager.UltimatePerformance &&
                     PowerManager.GetSchemes().Any(s => s.Id == PowerManager.HighPerformance))
            {
                state.PreviousScheme = scheme.Value.ToString();
                PowerManager.SetActiveScheme(PowerManager.HighPerformance);
                ActionLog.Success("Gaming Mode: Power plan", PowerManager.GetFriendlyName(scheme.Value), "High performance");
            }
        }
        catch (Exception ex)
        {
            messages.Add("Power: " + Safe.Describe(ex));
            ActionLog.Failed("Gaming Mode: Power", null, "Best performance", Safe.Describe(ex));
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
        await Task.CompletedTask;
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
        Changed?.Invoke();
    }
}
