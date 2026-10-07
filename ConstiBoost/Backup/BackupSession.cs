using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConstiBoost.WinSys;

namespace ConstiBoost.Backup;

/// <summary>
/// A backup folder (Backups/yyyy-MM-dd_HH-mm-ss/) that collects the state of everything that is
/// about to change. Every capture is written to disk immediately, before the change happens.
/// </summary>
public sealed class BackupSession
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    private BackupSession(string folder, BackupManifest manifest)
    {
        Folder = folder;
        Manifest = manifest;
    }

    public string Folder { get; }

    public BackupManifest Manifest { get; }

    public string ManifestPath => Path.Combine(Folder, "manifest.json");

    public static BackupSession Create(string title)
    {
        string id = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Core.AppPaths.Backups, id);
        int suffix = 1;
        while (Directory.Exists(folder))
        {
            folder = Path.Combine(Core.AppPaths.Backups, $"{id}_{suffix++}");
        }

        Directory.CreateDirectory(folder);
        var session = new BackupSession(folder, new BackupManifest
        {
            Id = Path.GetFileName(folder),
            Title = title,
            CreatedAt = DateTime.Now,
        });
        session.Save();
        return session;
    }

    public static BackupSession Load(string folder)
    {
        string json = File.ReadAllText(Path.Combine(folder, "manifest.json"));
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json) ?? new BackupManifest();
        return new BackupSession(folder, manifest);
    }

    public void CaptureRegistry(string hive, string keyPath, string valueName, string description)
    {
        lock (_gate)
        {
            bool already = Manifest.Entries.Any(e => e.Kind == BackupKinds.Registry && e.Registry != null &&
                string.Equals(e.Registry.Hive, hive, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Registry.KeyPath, keyPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Registry.ValueName, valueName, StringComparison.OrdinalIgnoreCase));
            if (already)
            {
                return;
            }

            Manifest.Entries.Add(new BackupEntry
            {
                Kind = BackupKinds.Registry,
                Description = description,
                Registry = RegistryValueSnapshot.Capture(hive, keyPath, valueName),
            });
            Save();
        }
    }

    public void CapturePowerScheme()
    {
        var active = PowerManager.GetActiveSchemeId();
        if (active is null)
        {
            throw new InvalidOperationException("The active power plan could not be read.");
        }

        AddOnce(BackupKinds.PowerScheme, "Previous power plan", active.Value.ToString(), PowerManager.GetFriendlyName(active.Value));
    }

    public void CapturePowerOverlay()
    {
        var overlay = PowerManager.GetOverlay();
        if (overlay is null)
        {
            throw new InvalidOperationException("The current power mode could not be read.");
        }

        AddOnce(BackupKinds.PowerOverlay, "Previous power mode", overlay.Value.ToString(), PowerManager.OverlayName(overlay));
    }

    public void CaptureSystemParameter(string name, string data, string description) =>
        AddOnce(BackupKinds.SystemParameter, description, data, name);

    public void CaptureFile(string path, string description)
    {
        lock (_gate)
        {
            if (Manifest.Entries.Any(e => e.Kind == BackupKinds.File && string.Equals(e.OriginalPath, path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string? backupName = null;
            if (File.Exists(path))
            {
                backupName = $"file_{Manifest.Entries.Count:D3}_{Path.GetFileName(path)}";
                File.Copy(path, Path.Combine(Folder, backupName), true);
            }

            Manifest.Entries.Add(new BackupEntry
            {
                Kind = BackupKinds.File,
                Description = description,
                OriginalPath = path,
                BackupFileName = backupName,
            });
            Save();
        }
    }

    public void MarkRestored()
    {
        lock (_gate)
        {
            Manifest.RestoredAt = DateTime.Now;
            Save();
        }
    }

    public bool IsEmpty => Manifest.Entries.Count == 0;

    private void AddOnce(string kind, string description, string data, string name)
    {
        lock (_gate)
        {
            if (Manifest.Entries.Any(e => e.Kind == kind && e.Name == name && kind != BackupKinds.PowerScheme && kind != BackupKinds.PowerOverlay))
            {
                return;
            }

            if ((kind == BackupKinds.PowerScheme || kind == BackupKinds.PowerOverlay) && Manifest.Entries.Any(e => e.Kind == kind))
            {
                return;
            }

            Manifest.Entries.Add(new BackupEntry { Kind = kind, Description = description, Data = data, Name = name });
            Save();
        }
    }

    private void Save()
    {
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(Manifest, JsonOptions));
    }
}
