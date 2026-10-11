using System;
using ConstiBoost.WinSys;

namespace ConstiBoost.Backup;

public static class BackupKinds
{
    public const string Registry = "Registry";
    public const string PowerScheme = "PowerScheme";
    public const string PowerOverlay = "PowerOverlay";
    public const string SystemParameter = "SystemParameter";
    public const string File = "File";
    public const string ProcessorMaxState = "ProcessorMaxState";
}

/// <summary>One recorded "before" state. Restoring an entry puts exactly this state back.</summary>
public sealed class BackupEntry
{
    public string Kind { get; set; } = BackupKinds.Registry;
    public string Description { get; set; } = string.Empty;
    public RegistryValueSnapshot? Registry { get; set; }
    public string? Name { get; set; }
    public string? Data { get; set; }
    public string? OriginalPath { get; set; }
    public string? BackupFileName { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;

    public bool RequiresAdmin => Kind == BackupKinds.Registry && Registry?.Hive == "HKLM";

    public string Summary => Kind switch
    {
        BackupKinds.Registry => $"{Description} ({Registry?.FullPath} = {Registry?.DisplayValue})",
        BackupKinds.PowerScheme => $"{Description} ({Data})",
        BackupKinds.PowerOverlay => $"{Description} ({Data})",
        BackupKinds.ProcessorMaxState => $"{Description} ({Name})",
        BackupKinds.File => $"{Description} ({OriginalPath})",
        _ => $"{Description} ({Name} = {Data})",
    };
}
