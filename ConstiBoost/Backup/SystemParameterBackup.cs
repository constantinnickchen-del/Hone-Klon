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

    public static string Capture(string name) => name switch
    {
        MinimizeAnimation => (SystemParametersTweaks.GetMinimizeAnimation() ?? throw new InvalidOperationException("Value unavailable")).ToString(),
        ClientAreaAnimation => (SystemParametersTweaks.GetClientAreaAnimation() ?? throw new InvalidOperationException("Value unavailable")).ToString(),
        Mouse => string.Join(",", (SystemParametersTweaks.GetMouseParams() ?? throw new InvalidOperationException("Value unavailable"))
            .Select(v => v.ToString(CultureInfo.InvariantCulture))),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static void Restore(string name, string data)
    {
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
