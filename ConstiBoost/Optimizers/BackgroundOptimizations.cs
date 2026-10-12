using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

public sealed class StartupAppsOptimization : Optimization
{
    private List<StartupEntry> _candidates = new();

    public override string Id => "startup";
    public override string Name => L.T("Optimize startup apps");
    public override string Category => L.T("Startup");
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;

    public override string WhatItDoes =>
        L.T("Disables autostart of known non-essential apps (game launchers, chat, music, browsers). They still work normally when you open them. System, driver and security programs are never touched.");

    public override string Downsides => L.T("These apps no longer start automatically – e.g. Discord or Steam must be opened manually. Re-enable any time on the Startup page.");

    protected override void RefreshCore(OptimizationContext context)
    {
        _candidates = StartupService.GetEntries().Where(e => e.IsEnabled && e.IsKnownNonEssential && !e.IsProtected).ToList();
        int enabled = StartupService.GetEntries().Count(e => e.IsEnabled && !e.IsProtected);
        CurrentState = L.F("{0} startup app(s) enabled", enabled);
        RecommendedState = _candidates.Count == 0 ? "No change needed" : L.T("Disable: ") + string.Join(", ", _candidates.Select(c => c.Name).Take(6)) + (_candidates.Count > 6 ? "…" : string.Empty);
        Reason = "Every autostart app uses RAM and CPU in the background – and makes boot slower.";
        NeedsChange = _candidates.Count > 0;
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        int ok = 0;
        var failed = new List<string>();
        foreach (var entry in _candidates)
        {
            var result = await StartupService.SetEnabledAsync(entry, false, backup);
            if (result.Success)
            {
                ok++;
            }
            else
            {
                failed.Add(entry.Name);
            }
        }

        return failed.Count == 0
            ? OperationResult.Ok(L.F("{0} startup app(s) disabled", ok))
            : OperationResult.Fail(L.F("{0} disabled, {1} failed", ok, failed.Count), string.Join(", ", failed));
    }
}

public sealed class CloseBackgroundAppsOptimization : Optimization
{
    private List<AppGroup> _running = new();

    public override string Id => "closebg";
    public override string Name => L.T("Reduce unnecessary background processes");
    public override string Category => L.T("Background");
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;
    public override bool IsReversible => false;

    public override string WhatItDoes =>
        L.T("Closes running background apps such as launchers, chat and sync clients (not browsers or open documents). For a reversible version use Gaming Mode, which reopens them afterwards.");

    public override string Downsides => L.T("Closed apps stop until you open them again (e.g. Discord voice chat, Spotify music).");

    public override bool PreselectWhenNeeded(OptimizationContext context) => false;

    protected override void RefreshCore(OptimizationContext context)
    {
        _running = BackgroundAppsService.GetCandidates().Where(g => g.IsKnownBackgroundApp).ToList();
        CurrentState = _running.Count == 0 ? L.T("None running") : string.Join(", ", _running.Select(r => r.DisplayName).Take(5)) + (_running.Count > 5 ? "…" : string.Empty);
        RecommendedState = _running.Count == 0 ? "Nothing to close" : L.F("Close {0} app(s) before gaming", _running.Count);
        Reason = "Frees RAM and CPU for the game. Most helpful on 8 GB RAM systems.";
        NeedsChange = _running.Count > 0;
    }

    protected override async Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        var messages = new List<string>();
        int closed = 0;
        foreach (var group in _running)
        {
            var (ok, message) = await BackgroundAppsService.CloseGroupAsync(group);
            if (ok)
            {
                closed++;
            }
            else
            {
                messages.Add(message);
            }
        }

        return messages.Count == 0
            ? OperationResult.Ok(L.F("{0} app(s) closed", closed))
            : OperationResult.Fail(L.F("{0} closed, {1} not closed", closed, messages.Count), string.Join(" ", messages));
    }
}
