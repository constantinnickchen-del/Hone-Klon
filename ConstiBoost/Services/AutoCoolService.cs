using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

public sealed class AutoCoolState
{
    public DateTime EngagedAt { get; set; }
    public string? PreviousOverlay { get; set; }
    public string? PreviousScheme { get; set; }
    public string? ProcessorScheme { get; set; }
    public uint? PreviousMaxAc { get; set; }
    public uint? PreviousMaxDc { get; set; }
}

/// <summary>
/// ❄️ Auto-Cool guard (optional): if the temperature stays above the trigger for 60 s, ConstiBoost switches to the
/// Balanced power mode (and optionally turns off CPU boost). When it has been cooler for 2 minutes, everything is
/// restored. It never touches fans, voltages or thermal protection and never shows a UAC prompt on its own.
/// </summary>
public sealed class AutoCoolService
{
    private static string StateFile => Path.Combine(AppPaths.Root, "autocool-state.json");

    private readonly object _gate = new();
    private DateTime? _hotSince;
    private DateTime? _coolSince;
    private AutoCoolState? _state;
    private bool _working;

    public static AutoCoolService Instance { get; } = new();

    public bool IsEngaged => _state != null;

    public DateTime? EngagedAt => _state?.EngagedAt;

    public event Action? Changed;

    /// <summary>Restores a guard left engaged by a crash/shutdown.</summary>
    public void RestoreLeftover()
    {
        try
        {
            if (File.Exists(StateFile))
            {
                _state = JsonSerializer.Deserialize<AutoCoolState>(File.ReadAllText(StateFile));
                _ = DisengageAsync("ConstiBoost restarted");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Auto-Cool state unreadable: " + ex.Message);
        }
    }

    public void Evaluate(ThermalSample sample)
    {
        var s = SettingsService.Instance.Current;
        if (!s.AutoCoolEnabled)
        {
            if (_state != null)
            {
                _ = DisengageAsync("Auto-Cool turned off");
            }

            return;
        }

        if (sample.Temperature is not double t)
        {
            return;
        }

        int trigger = Math.Clamp(s.AutoCoolTrigger, 75, 95);
        lock (_gate)
        {
            if (_working)
            {
                return;
            }

            if (_state is null)
            {
                _hotSince = t >= trigger ? _hotSince ?? sample.Time : null;
                bool blocked = GamingModeService.Instance.IsActive || (sample.GameRunning && !s.AutoCoolDuringGaming);
                if (!blocked && _hotSince.HasValue && sample.Time - _hotSince.Value >= TimeSpan.FromSeconds(60))
                {
                    _working = true;
                    _ = EngageAsync(trigger, s.AutoCoolLimitBoost);
                }
            }
            else
            {
                _coolSince = t <= trigger - 8 ? _coolSince ?? sample.Time : null;
                if (_coolSince.HasValue && sample.Time - _coolSince.Value >= TimeSpan.FromMinutes(2))
                {
                    _working = true;
                    _ = DisengageAsync("temperature back to normal");
                }
            }
        }
    }

    private async Task EngageAsync(int trigger, bool limitBoost)
    {
        try
        {
            var state = new AutoCoolState { EngagedAt = DateTime.Now };
            var scheme = PowerManager.GetActiveSchemeId();
            var overlay = scheme == PowerManager.Balanced ? PowerManager.GetOverlay() : null;
            bool changed = false;
            if (overlay != null && overlay != PowerManager.OverlayBalanced && overlay != PowerManager.OverlayBestEfficiency)
            {
                state.PreviousOverlay = overlay.Value.ToString();
                PowerManager.SetOverlay(PowerManager.OverlayBalanced);
                ActionLog.Success("Auto-Cool: power mode", PowerManager.OverlayName(overlay), "Balanced");
                changed = true;
            }
            else if (scheme == PowerManager.HighPerformance || scheme == PowerManager.UltimatePerformance)
            {
                state.PreviousScheme = scheme.Value.ToString();
                PowerManager.SetActiveScheme(PowerManager.Balanced);
                ActionLog.Success("Auto-Cool: power plan", PowerManager.GetFriendlyName(scheme.Value), "Balanced");
                changed = true;
            }

            var active = PowerManager.GetActiveSchemeId();
            if (limitBoost && active is Guid a && PowerManager.GetProcessorMaxState(a) is { } max && (max.ac > 99 || max.dc > 99))
            {
                var r = await PowerTweakService.SetProcessorMaxStateAsync(a, 99, 99, "Auto-Cool: CPU boost off", allowElevation: false);
                if (r.Success)
                {
                    state.ProcessorScheme = a.ToString();
                    state.PreviousMaxAc = max.ac;
                    state.PreviousMaxDc = max.dc;
                    changed = true;
                }
            }

            if (!changed)
            {
                // Already in the coolest software settings – nothing to do; remind about cooling instead.
                NotificationService.Instance.Show(L.F("❄️ Auto-Cool: temperature stayed above {0} °C, but the power settings are already balanced. Check CPU load and cooling conditions.", trigger), NotificationKind.Warning);
                _hotSince = null;
                return;
            }

            _state = state;
            File.WriteAllText(StateFile, JsonSerializer.Serialize(state));
            NotificationService.Instance.Show(L.F("❄️ Auto-Cool engaged: the temperature stayed above {0} °C for a minute, so ConstiBoost switched to cooler power settings. They are restored automatically when it cools down.", trigger), NotificationKind.Info);
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Error("Auto-Cool engage failed", ex);
        }
        finally
        {
            _working = false;
        }
    }

    public async Task DisengageAsync(string reason)
    {
        var state = _state;
        if (state is null)
        {
            _working = false;
            return;
        }

        try
        {
            if (state.PreviousOverlay != null)
            {
                PowerManager.SetOverlay(Guid.Parse(state.PreviousOverlay));
            }

            if (state.PreviousScheme != null)
            {
                PowerManager.SetActiveScheme(Guid.Parse(state.PreviousScheme));
            }

            if (state.ProcessorScheme != null && state.PreviousMaxAc is uint ac && state.PreviousMaxDc is uint dc)
            {
                await PowerTweakService.SetProcessorMaxStateAsync(Guid.Parse(state.ProcessorScheme), ac, dc, "Auto-Cool: restore CPU boost", allowElevation: false);
            }

            ActionLog.Success("Auto-Cool released", "Engaged", $"Restored ({reason})");
            NotificationService.Instance.Show(L.F("❄️ Auto-Cool released ({0}) – previous power settings restored.", L.T(reason)), NotificationKind.Info);
        }
        catch (Exception ex)
        {
            AppLog.Error("Auto-Cool restore failed", ex);
        }
        finally
        {
            _state = null;
            _coolSince = null;
            _hotSince = null;
            Safe.Try(() => File.Delete(StateFile));
            _working = false;
            Changed?.Invoke();
        }
    }
}
