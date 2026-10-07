using System;
using System.Windows;
using System.Windows.Media;

namespace ConstiBoost.Services;

/// <summary>Swaps theme and accent brushes at runtime (all XAML uses DynamicResource).</summary>
public static class ThemeService
{
    public static readonly string[] AccentPresets =
    {
        "#00C2FF", "#7C5CFF", "#2EE59D", "#FF8A3D", "#FF4D6D", "#F5F5F5",
    };

    public static void Apply(string theme, string accentHex)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        bool midnight = string.Equals(theme, "Midnight", StringComparison.OrdinalIgnoreCase);
        Set(resources, "BackgroundBrush", midnight ? "#000000" : "#0B0C0F");
        Set(resources, "SidebarBrush", midnight ? "#050506" : "#0F1014");
        Set(resources, "CardBrush", midnight ? "#0D0D10" : "#16171C");
        Set(resources, "CardHoverBrush", midnight ? "#141418" : "#1C1D23");
        Set(resources, "CardBorderBrush", midnight ? "#1A1B20" : "#24262D");
        Set(resources, "InputBrush", midnight ? "#121216" : "#1E1F25");

        Color accent;
        try
        {
            accent = (Color)ColorConverter.ConvertFromString(accentHex);
        }
        catch
        {
            accent = (Color)ColorConverter.ConvertFromString(AccentPresets[0]);
        }

        resources["AccentColor"] = accent;
        resources["AccentBrush"] = Freeze(new SolidColorBrush(accent));
        resources["AccentSoftBrush"] = Freeze(new SolidColorBrush(Color.FromArgb(0x26, accent.R, accent.G, accent.B)));
        resources["AccentGlowBrush"] = Freeze(new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B)));
        double luminance = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255;
        resources["OnAccentBrush"] = Freeze(new SolidColorBrush(luminance > 0.6 ? Color.FromRgb(0x0B, 0x0C, 0x0F) : Colors.White));
    }

    private static void Set(ResourceDictionary resources, string key, string hex) =>
        resources[key] = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
