using System;
using System.IO;

namespace ConstiBoost.Core;

/// <summary>
/// All data ConstiBoost writes lives in %LOCALAPPDATA%\ConstiBoost so the app never needs admin
/// rights for its own files and never touches the install directory.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConstiBoost");

    public static string Backups => Ensure(Path.Combine(Root, "Backups"));

    public static string Logs => Ensure(Path.Combine(Root, "Logs"));

    public static string Temp => Ensure(Path.Combine(Root, "Temp"));

    public static string SettingsFile => Path.Combine(Ensure(Root), "settings.json");

    public static string BenchmarkFile => Path.Combine(Ensure(Root), "benchmarks.json");

    public static string ThermalTestsFile => Path.Combine(Ensure(Root), "thermal-tests.json");

    public static string GamingModeStateFile => Path.Combine(Ensure(Root), "gamingmode-state.json");

    public static string ExecutablePath => Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "ConstiBoost.exe");

    private static string Ensure(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch
        {
            // Directory creation failures surface later as handled IO errors.
        }

        return path;
    }
}
