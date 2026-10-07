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
}
