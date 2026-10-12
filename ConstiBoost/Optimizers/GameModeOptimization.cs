using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

public sealed class GameModeOptimization : Optimization
{
    public override string Id => "gamemode";
    public override string Name => L.T("Game Mode");
    public override string Category => L.T("Gaming");
    public override string Icon => "";
    public override ImpactLevel Impact => ImpactLevel.Medium;

    public override string WhatItDoes =>
        L.T("Windows Game Mode prioritizes the game you are playing: it prevents Windows Update from installing drivers or showing restart notifications while you play and gives the game more consistent CPU time.");

    public override string Downsides =>
        L.T("Practically none. Background tasks (e.g. streaming or recording software) may get slightly less CPU time while a game runs.");

    protected override void RefreshCore(OptimizationContext context)
    {
        bool on = GameFeatures.GameModeEnabled;
        CurrentState = OnOff(on);
        RecommendedState = "ON";
        Reason = L.T("Windows can treat games preferentially and avoids update interruptions during gaming.");
        NeedsChange = !on;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context) =>
        Task.FromResult(SetUserDword(backup, GameFeatures.GameBarKey, GameFeatures.GameModeValue, 1, CurrentState, "ON"));
}
