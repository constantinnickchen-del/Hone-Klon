using System;
using System.Globalization;
using ConstiBoost.Localization;

namespace ConstiBoost.Core;

public static class Formatting
{
    public static string Unavailable => L.T("Unavailable");

    public static string Bytes(long bytes)
    {
        if (bytes < 0)
        {
            return Unavailable;
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    public static string Megabytes(long bytes) =>
        (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

    public static string Percent(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) ? Math.Round(value.Value).ToString(CultureInfo.InvariantCulture) + " %" : Unavailable;

    public static string Celsius(double? value) =>
        value.HasValue ? Math.Round(value.Value).ToString(CultureInfo.InvariantCulture) + " °C" : Unavailable;
}
