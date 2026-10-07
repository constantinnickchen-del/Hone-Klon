using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ConstiBoost.Models;

namespace ConstiBoost.UI;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && (v == Visibility.Visible) != Invert;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            null => false,
            string s => s.Length > 0,
            ICollection c => c.Count > 0,
            _ => true,
        };
        if (Invert)
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => !(value is bool b && b);
}

/// <summary>Maps statuses (CheckStatus, PerformanceStatus, ImpactLevel, bool? success) to theme brushes.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public bool Soft { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            NotificationKind.Success => "Good",
            NotificationKind.Warning => "Warn",
            NotificationKind.Error => "Bad",
            NotificationKind.Info => "Accent",
            CheckStatus.Good or PerformanceStatus.Good or true => "Good",
            CheckStatus.Warning or PerformanceStatus.CouldBeImproved => "Warn",
            CheckStatus.Bad or PerformanceStatus.NeedsAttention or false => "Bad",
            ImpactLevel.High => "Good",
            ImpactLevel.Medium => "Warn",
            ImpactLevel.Low => "Neutral",
            string s when s.Equals("Optimal", StringComparison.OrdinalIgnoreCase) || s.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) => "Good",
            string s when s.Equals("Can be improved", StringComparison.OrdinalIgnoreCase) || s.Equals("Your choice", StringComparison.OrdinalIgnoreCase) => "Warn",
            string s when s.Equals("FAILED", StringComparison.OrdinalIgnoreCase) => "Bad",
            _ => "Neutral",
        };

        if (Soft && key != "Neutral" && key != "Accent")
        {
            key += "Soft";
        }

        return Application.Current?.TryFindResource(key + "Brush") as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not double width)
        {
            return 0.0;
        }

        double pct = values[0] switch
        {
            double d => d,
            int i => i,
            _ => 0,
        };
        return Math.Max(0, Math.Min(1, pct / 100)) * width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => Array.Empty<object>();
}
