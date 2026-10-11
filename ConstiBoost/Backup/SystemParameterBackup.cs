using System;
using System.Globalization;
using System.Linq;
using ConstiBoost.WinSys;

namespace ConstiBoost.Backup;

/// <summary>Serializes SystemParametersInfo values for backups.</summary>
public static class SystemParameterBackup
{
    public const string MinimizeAnimation = "MinimizeAnimation";
    public const string ClientAreaAnimation = "ClientAreaAnimation";
    public const string Mouse = "Mouse";

    /// <summary>Generic boolean SPI entry: "Spi:{get}:{set}:{uiParam}".</summary>
    public static string SpiName(uint get, uint set, bool uiParam) => $"Spi:{get}:{set}:{(uiParam ? 1 : 0)}";

    public static string Capture(string name) => name.StartsWith("Spi:", StringComparison.Ordinal)
        ? (SystemParametersTweaks.GetBool(uint.Parse(name.Split(':')[1], CultureInfo.InvariantCulture)) ?? throw new InvalidOperationException("Value unavailable")).ToString()
        : name switch
    {
        MinimizeAnimation => (SystemParametersTweaks.GetMinimizeAnimation() ?? throw new InvalidOperationException("Value unavailable")).ToString(),
        ClientAreaAnimation => (SystemParametersTweaks.GetClientAreaAnimation() ?? throw new InvalidOperationException("Value unavailable")).ToString(),
        Mouse => string.Join(",", (SystemParametersTweaks.GetMouseParams() ?? throw new InvalidOperationException("Value unavailable"))
            .Select(v => v.ToString(CultureInfo.InvariantCulture))),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static void Restore(string name, string data)
    {
        if (name.StartsWith("Spi:", StringComparison.Ordinal))
        {
            var parts = name.Split(':');
            SystemParametersTweaks.SetBool(uint.Parse(parts[2], CultureInfo.InvariantCulture), bool.Parse(data), parts[3] == "1");
            return;
        }

        switch (name)
        {
            case MinimizeAnimation:
                SystemParametersTweaks.SetMinimizeAnimation(bool.Parse(data));
                break;
            case ClientAreaAnimation:
                SystemParametersTweaks.SetClientAreaAnimation(bool.Parse(data));
                break;
            case Mouse:
                SystemParametersTweaks.SetMouseParams(data.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }
}
