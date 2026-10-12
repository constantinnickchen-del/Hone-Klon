using System;

namespace ConstiBoost.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#00C2FF";
    public bool StartWithWindows { get; set; }
    public bool CreateRestorePointBeforeMajorChanges { get; set; } = true;
    public bool AutomaticBackup { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool FirstRunCompleted { get; set; }
    public bool GamingModeRelaunchClosedApps { get; set; } = true;
    public bool ThermalMonitoringEnabled { get; set; }
    public int ThermalHighThreshold { get; set; } = 85;
    public int ThermalCriticalThreshold { get; set; } = 95;
    public int ThermalSustainSeconds { get; set; } = 45;
    public bool ThermalDeepScan { get; set; }
    public bool AutoCoolEnabled { get; set; }
    public int AutoCoolTrigger { get; set; } = 88;
    public bool AutoCoolDuringGaming { get; set; }
    public bool AutoCoolLimitBoost { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
    public string OverlayCorner { get; set; } = "TopLeft";
    public bool OverlayShowsFps { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public bool WeeklyMaintenance { get; set; }
    public DateTime? LastMaintenance { get; set; }
    public GamingProfile GamingProfile { get; set; } = GamingProfile.MaxFps;
    public string BrowserLaunchUrl { get; set; } = string.Empty;
    public int BrowserRenderScale { get; set; } = 75;
    public bool BrowserUnlockFps { get; set; }
    public bool BrowserLaunchStartsFocus { get; set; } = true;
}
