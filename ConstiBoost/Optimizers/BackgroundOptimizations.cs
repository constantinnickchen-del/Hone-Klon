using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.Optimizers;

public sealed class StartupAppsOptimization : Optimization
{
    private List<StartupEntry> _candidates = new();

    public override string Id => "startup";
    public override string Name => "Optimize startup apps";
    public override string Category => "Startup";
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;

    public override string WhatItDoes =>
        "Disables autostart of known non-essential apps (game launchers, chat, music, browsers). They still work normally when you open them. System, driver and security programs are never touched.";

    public override string Downsides => "These apps no longer start automatically – e.g. Discord or Steam must be opened manually. Re-enable any time on the Startup page.";

    protected override void RefreshCore(OptimizationContext context)
    {
        _candidates = StartupService.GetEntries().Where(e => e.IsEnabled && e.IsKnownNonEssential && !e.IsProtected).ToList();
        int enabled = StartupService.GetEntries().Count(e => e.IsEnabled && !e.IsProtected);
        CurrentState = $"{enabled} startup app(s) enabled";
        RecommendedState = _candidates.Count == 0 ? "No change needed" : "Disable: " + string.Join(", ", _candidates.Select(c => c.Name).Take(6)) + (_candidates.Count > 6 ? "…" : string.Empty);
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
            ? OperationResult.Ok($"{ok} startup app(s) disabled")
            : OperationResult.Fail($"{ok} disabled, {failed.Count} failed", string.Join(", ", failed));
    }
}

public sealed class CloseBackgroundAppsOptimization : Optimization
{
    private List<AppGroup> _running = new();

    public override string Id => "closebg";
    public override string Name => "Reduce unnecessary background processes";
    public override string Category => "Background";
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;
    public override bool IsReversible => false;

    public override string WhatItDoes =>
        "Closes running background apps such as launchers, chat and sync clients (not browsers or open documents). For a reversible version use Gaming Mode, which reopens them afterwards.";

    public override string Downsides => "Closed apps stop until you open them again (e.g. Discord voice chat, Spotify music).";

    public override bool PreselectWhenNeeded(OptimizationContext context) => false;

    protected override void RefreshCore(OptimizationContext context)
    {
        _running = BackgroundAppsService.GetCandidates().Where(g => g.IsKnownBackgroundApp).ToList();
        CurrentState = _running.Count == 0 ? "None running" : string.Join(", ", _running.Select(r => r.DisplayName).Take(5)) + (_running.Count > 5 ? "…" : string.Empty);
        RecommendedState = _running.Count == 0 ? "Nothing to close" : $"Close {_running.Count} app(s) before gaming";
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
            ? OperationResult.Ok($"{closed} app(s) closed")
            : OperationResult.Fail($"{closed} closed, {messages.Count} not closed", string.Join(" ", messages));
    }
}
