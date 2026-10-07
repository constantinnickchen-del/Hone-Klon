using System;
using System.Globalization;
using System.Linq;
using Microsoft.Win32;

namespace ConstiBoost.WinSys;

/// <summary>Serializable copy of a single registry value (or the fact that it did not exist).</summary>
public sealed class RegistryValueSnapshot
{
    public string Hive { get; set; } = "HKCU";
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public bool Existed { get; set; }
    public string Kind { get; set; } = nameof(RegistryValueKind.DWord);
    public string? Data { get; set; }

    public string FullPath => $@"{Hive}\{KeyPath}\{ValueName}";

    public string DisplayValue => Existed ? (Data ?? string.Empty) : "(not set)";

    public static RegistryValueSnapshot Capture(string hive, string keyPath, string valueName)
    {
        var snapshot = new RegistryValueSnapshot { Hive = hive, KeyPath = keyPath, ValueName = valueName };
        using var root = RegistryHelper.OpenBase(hive);
        using var key = root.OpenSubKey(keyPath, false);
        object? value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (key is null || value is null)
        {
            snapshot.Existed = false;
            return snapshot;
        }

        var kind = key.GetValueKind(valueName);
        snapshot.Existed = true;
        snapshot.Kind = kind.ToString();
        snapshot.Data = Serialize(value, kind);
        return snapshot;
    }

    public void Restore()
    {
        using var root = RegistryHelper.OpenBase(Hive);
        if (!Existed)
        {
            using var key = root.OpenSubKey(KeyPath, true);
            key?.DeleteValue(ValueName, false);
            return;
        }

        using var writable = root.CreateSubKey(KeyPath, true)
            ?? throw new InvalidOperationException("Registry key could not be opened: " + KeyPath);
        var kind = Enum.Parse<RegistryValueKind>(Kind);
        writable.SetValue(ValueName, Deserialize(Data, kind), kind);
    }

    public static string Serialize(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
        RegistryValueKind.MultiString => string.Join("\u0000", (string[])value),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    public static object Deserialize(string? data, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => int.Parse(data ?? "0", CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => long.Parse(data ?? "0", CultureInfo.InvariantCulture),
        RegistryValueKind.Binary => Convert.FromBase64String(data ?? string.Empty),
        RegistryValueKind.MultiString => (data ?? string.Empty).Split('\u0000').Where(s => s.Length > 0).ToArray(),
        _ => data ?? string.Empty,
    };
}
