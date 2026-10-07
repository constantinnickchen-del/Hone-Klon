using System;
using Microsoft.Win32;

namespace ConstiBoost.WinSys;

public static class RegistryHelper
{
    public static RegistryKey OpenBase(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, "Only HKCU and HKLM are supported."),
    };

    public static int? ReadDword(string hive, string keyPath, string valueName)
    {
        try
        {
            using var root = OpenBase(hive);
            using var key = root.OpenSubKey(keyPath, false);
            object? value = key?.GetValue(valueName);
            return value switch
            {
                int i => i,
                long l => (int)l,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    public static string? ReadString(string hive, string keyPath, string valueName)
    {
        try
        {
            using var root = OpenBase(hive);
            using var key = root.OpenSubKey(keyPath, false);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    public static bool KeyExists(string hive, string keyPath)
    {
        try
        {
            using var root = OpenBase(hive);
            using var key = root.OpenSubKey(keyPath, false);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void WriteDword(string hive, string keyPath, string valueName, int value)
    {
        using var root = OpenBase(hive);
        using var key = root.CreateSubKey(keyPath, true)
            ?? throw new InvalidOperationException("Registry key could not be opened: " + keyPath);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
