using System.Collections.Generic;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

public static class ElevatedOperations
{
    public const string RegistryRestore = "registry-restore";
    public const string RegistrySetDword = "registry-set-dword";
    public const string CleanPaths = "clean-paths";
    public const string CreateRestorePoint = "create-restore-point";
}

public sealed class DwordWrite
{
    public string Hive { get; set; } = "HKLM";
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>Work item handed to the short-lived elevated ConstiBoost instance.</summary>
public sealed class ElevatedTask
{
    public string Operation { get; set; } = string.Empty;
    public List<RegistryValueSnapshot> Snapshots { get; set; } = new();
    public List<DwordWrite> DwordWrites { get; set; } = new();
    public List<string> Paths { get; set; } = new();
    public string? Description { get; set; }
    public double MinAgeHours { get; set; }
}

public sealed class ElevatedResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long FreedBytes { get; set; }
    public int DeletedFiles { get; set; }
    public int SkippedFiles { get; set; }
}
