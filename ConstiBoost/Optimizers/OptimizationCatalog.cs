using System.Collections.Generic;

namespace ConstiBoost.Optimizers;

/// <summary>All optimizations ConstiBoost offers. Deliberately no "RAM booster", "network booster" or other placebo tweaks.</summary>
public static class OptimizationCatalog
{
    public static List<Optimization> CreateAll() => new()
    {
        new GameModeOptimization(),
        new PowerOptimization(),
        new BackgroundRecordingOptimization(),
        new GameDvrOptimization(),
        new StartupAppsOptimization(),
        new CloseBackgroundAppsOptimization(),
        new TempFilesOptimization(),
        new ShaderCacheOptimization(),
        new TransparencyOptimization(),
        new AnimationsOptimization(),
        new BackgroundAppsOptimization(),
        new MouseAccelerationOptimization(),
        new HagsOptimization(),
    };
}
