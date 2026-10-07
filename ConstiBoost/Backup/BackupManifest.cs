using System;
using System.Collections.Generic;

namespace ConstiBoost.Backup;

public sealed class BackupManifest
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? RestoredAt { get; set; }
    public string AppVersion { get; set; } = "1.0.0";
    public List<BackupEntry> Entries { get; set; } = new();
}
