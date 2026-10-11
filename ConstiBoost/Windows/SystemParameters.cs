using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ConstiBoost.WinSys;

/// <summary>Documented SystemParametersInfo settings (animations, mouse acceleration).</summary>
public static class SystemParametersTweaks
{
    private const uint Persist = NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE;

    public static bool? GetMinimizeAnimation()
    {
        var info = new NativeMethods.ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.ANIMATIONINFO>() };
        return NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETANIMATION, info.cbSize, ref info, 0)
            ? info.iMinAnimate != 0
            : null;
    }

    public static void SetMinimizeAnimation(bool enabled)
    {
        var info = new NativeMethods.ANIMATIONINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.ANIMATIONINFO>(),
            iMinAnimate = enabled ? 1 : 0,
        };
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETANIMATION, info.cbSize, ref info, Persist))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static bool? GetClientAreaAnimation()
    {
        int value = 0;
        return NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETCLIENTAREAANIMATION, 0, ref value, 0) ? value != 0 : null;
    }

    public static void SetClientAreaAnimation(bool enabled)
    {
        // For SPI_SETCLIENTAREAANIMATION the BOOL is passed directly in pvParam.
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCLIENTAREAANIMATION, 0, enabled ? new System.IntPtr(1) : System.IntPtr.Zero, Persist))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Boolean SPI settings whose SET call takes the value in pvParam (menu/tooltip/combobox animations, smooth scrolling…).</summary>
    public static bool? GetBool(uint getAction)
    {
        int value = 0;
        return NativeMethods.SystemParametersInfo(getAction, 0, ref value, 0) ? value != 0 : null;
    }

    public static void SetBool(uint setAction, bool enabled, bool valueInUiParam = false)
    {
        bool ok = valueInUiParam
            ? NativeMethods.SystemParametersInfo(setAction, enabled ? 1u : 0u, System.IntPtr.Zero, Persist)
            : NativeMethods.SystemParametersInfo(setAction, 0, enabled ? new System.IntPtr(1) : System.IntPtr.Zero, Persist);
        if (!ok)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>Returns [threshold1, threshold2, acceleration]; acceleration 1 = "Enhance pointer precision" on.</summary>
    public static int[]? GetMouseParams()
    {
        var values = new int[3];
        return NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETMOUSE, 0, values, 0) ? values : null;
    }

    public static void SetMouseParams(int[] values)
    {
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETMOUSE, 0, values, Persist))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
