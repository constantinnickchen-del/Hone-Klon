using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.Localization;

namespace ConstiBoost.Optimizers;

public sealed class TempFilesOptimization : Optimization
{
    private long _bytes;

    public override string Id => "tempfiles";
    public override string Name => L.T("Clear temporary files");
    public override string Category => L.T("Cleanup");
    public override string Icon => "";
    public override bool IsReversible => false;

    public override string WhatItDoes => L.T("Deletes files older than 24 hours from your user temp folder (%TEMP%). Files in use are skipped.");

    public override string Downsides => L.T("None for games. Deleted temp files cannot be restored (they are not personal files).");

    public override bool PreselectWhenNeeded(OptimizationContext context) => _bytes > 100L * 1024 * 1024;

    protected override void RefreshCore(OptimizationContext context)
    {
        (_bytes, int files) = CleanerService.Measure(Path.GetTempPath(), 24);
        CurrentState = L.F("{0} ({1} files)", Formatting.Bytes(_bytes), files);
        RecommendedState = "Cleaned";
        Reason = L.T("Frees disk space. Helps most when the system drive is nearly full.");
        NeedsChange = _bytes > 50L * 1024 * 1024;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        var result = CleanerService.DeleteContents(Path.GetTempPath(), 24);
        ActionLog.Success(Name, CurrentState, $"{Formatting.Bytes(result.FreedBytes)} freed, {result.SkippedFiles} skipped");
        return Task.FromResult(OperationResult.Ok(L.F("{0} removed", Formatting.Bytes(result.FreedBytes))));
    }
}

public sealed class ShaderCacheOptimization : Optimization
{
    private long _bytes;

    public override string Id => "shadercache";
    public override string Name => L.T("Clear safe shader caches");
    public override string Category => L.T("Cleanup");
    public override string Icon => "";
    public override bool IsReversible => false;

    public override string WhatItDoes => L.T("Deletes the DirectX (D3DSCache) and GPU driver (AMD/NVIDIA/Intel) shader caches. They are rebuilt automatically.");

    public override string Downsides =>
        L.T("The first match / first game start afterwards can stutter while shaders are compiled again. Only useful after driver updates or with permanent stutter / a very large cache.");

    public override bool PreselectWhenNeeded(OptimizationContext context) => _bytes > 1024L * 1024 * 1024;

    protected override void RefreshCore(OptimizationContext context)
    {
        _bytes = CleanerService.ShaderCachePaths().Sum(p => CleanerService.Measure(p, 0).bytes);
        CurrentState = Formatting.Bytes(_bytes);
        RecommendedState = _bytes > 1024L * 1024 * 1024 ? "Cleaned" : L.T("Keep (cache is small)");
        Reason = _bytes > 1024L * 1024 * 1024
            ? L.T("Cache is larger than 1 GB – rebuilding can fix stutter from outdated entries.")
            : L.T("A small cache is healthy; deleting it would only cause extra compile stutter.");
        NeedsChange = _bytes > 1024L * 1024 * 1024;
    }

    protected override Task<OperationResult> ApplyCoreAsync(BackupSession backup, OptimizationContext context)
    {
        long freed = 0;
        int skipped = 0;
        foreach (string path in CleanerService.ShaderCachePaths())
        {
            var r = CleanerService.DeleteContents(path);
            freed += r.FreedBytes;
            skipped += r.SkippedFiles;
        }

        ActionLog.Success(Name, CurrentState, $"{Formatting.Bytes(freed)} freed, {skipped} skipped");
        return Task.FromResult(OperationResult.Ok(L.F("{0} removed", Formatting.Bytes(freed))));
    }
}
