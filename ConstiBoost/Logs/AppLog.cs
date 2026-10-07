using System;
using System.Globalization;
using System.IO;

namespace ConstiBoost.Logging;

/// <summary>Technical background log (exceptions, warnings). Never contains personal data.</summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static string CurrentFile =>
        Path.Combine(Core.AppPaths.Logs, $"technical-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            string line = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}] {level} {message}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(CurrentFile, line);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
