using System;
using System.IO;
using System.Text.Json;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using Microsoft.Win32;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

public sealed class SettingsService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "ConstiBoost";

    public static SettingsService Instance { get; } = new();

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Settings could not be read, defaults are used: " + ex.Message);
            Current = new AppSettings();
        }

        Current.StartWithWindows = IsStartWithWindowsEnabled();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Settings could not be saved: " + ex.Message);
        }
    }

    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(RunValue) is string;
        }
        catch
        {
            return false;
        }
    }

    public OperationResult SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            bool before = key.GetValue(RunValue) is string;
            if (enabled)
            {
                key.SetValue(RunValue, $"\"{AppPaths.ExecutablePath}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(RunValue, false);
            }

            ActionLog.Success("Start ConstiBoost with Windows", before ? "ON" : "OFF", enabled ? "ON" : "OFF");
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Start ConstiBoost with Windows", null, enabled ? "ON" : "OFF", Safe.Describe(ex));
            return OperationResult.Fail(L.T("This setting couldn't be applied."), ex.Message);
        }
    }
}
