using System;
using System.Threading.Tasks;
using ConstiBoost.Logging;

using ConstiBoost.Localization;

namespace ConstiBoost.Core;

/// <summary>Helpers that wrap system calls so a missing permission or API never crashes the app.</summary>
public static class Safe
{
    public static T Try<T>(Func<T> action, T fallback, string? context = null)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            if (context != null)
            {
                AppLog.Warn($"{context}: {ex.GetType().Name}: {ex.Message}");
            }

            return fallback;
        }
    }

    public static bool Try(Action action, string? context = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            if (context != null)
            {
                AppLog.Warn($"{context}: {ex.GetType().Name}: {ex.Message}");
            }

            return false;
        }
    }

    public static async Task<T> TryAsync<T>(Func<Task<T>> action, T fallback, string? context = null)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (context != null)
            {
                AppLog.Warn($"{context}: {ex.GetType().Name}: {ex.Message}");
            }

            return fallback;
        }
    }

    /// <summary>Converts an exception into a short, user-facing reason.</summary>
    public static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Access denied",
        System.Security.SecurityException => "Access denied",
        System.IO.IOException io => L.T("File or resource in use: ") + io.Message,
        System.ComponentModel.Win32Exception w when w.NativeErrorCode == 5 => "Access denied",
        System.ComponentModel.Win32Exception w when w.NativeErrorCode == 1223 => L.T("Administrator permission was declined"),
        _ => ex.Message,
    };
}
