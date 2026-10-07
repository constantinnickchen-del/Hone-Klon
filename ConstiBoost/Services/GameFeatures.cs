using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

/// <summary>Registry locations of documented Windows gaming features (all per-user unless noted).</summary>
public static class GameFeatures
{
    public const string GameBarKey = @"Software\Microsoft\GameBar";
    public const string GameModeValue = "AutoGameModeEnabled";
    public const string GameBarControllerValue = "UseNexusForGameBarEnabled";

    public const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    public const string AppCaptureValue = "AppCaptureEnabled";
    public const string HistoricalCaptureValue = "HistoricalCaptureEnabled";

    public const string GameConfigStoreKey = @"System\GameConfigStore";
    public const string GameDvrEnabledValue = "GameDVR_Enabled";

    public const string GraphicsDriversKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers"; // HKLM
    public const string HagsValue = "HwSchMode";

    public const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string TransparencyValue = "EnableTransparency";

    public const string BackgroundAppsKey = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    public const string BackgroundAppsValue = "GlobalUserDisabled";

    /// <summary>Game Mode is on by default on Windows 10 1809+ and Windows 11 when the value is missing.</summary>
    public static bool GameModeEnabled => (RegistryHelper.ReadDword("HKCU", GameBarKey, GameModeValue) ?? 1) != 0;

    public static bool GameBarControllerEnabled => (RegistryHelper.ReadDword("HKCU", GameBarKey, GameBarControllerValue) ?? 1) != 0;

    public static bool GameDvrEnabled =>
        (RegistryHelper.ReadDword("HKCU", GameDvrKey, AppCaptureValue) ?? 1) != 0 &&
        (RegistryHelper.ReadDword("HKCU", GameConfigStoreKey, GameDvrEnabledValue) ?? 1) != 0;

    public static bool BackgroundRecordingEnabled => (RegistryHelper.ReadDword("HKCU", GameDvrKey, HistoricalCaptureValue) ?? 0) != 0;

    /// <summary>null = the graphics driver/Windows does not expose the setting.</summary>
    public static bool? HagsEnabled
    {
        get
        {
            int? value = RegistryHelper.ReadDword("HKLM", GraphicsDriversKey, HagsValue);
            return value is null ? null : value == 2;
        }
    }

    public static bool TransparencyEnabled => (RegistryHelper.ReadDword("HKCU", PersonalizeKey, TransparencyValue) ?? 1) != 0;

    public static bool BackgroundAppsDisabled => (RegistryHelper.ReadDword("HKCU", BackgroundAppsKey, BackgroundAppsValue) ?? 0) == 1;
}
