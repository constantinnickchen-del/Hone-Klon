using System;
using System.Runtime.InteropServices;

namespace ConstiBoost.WinSys;

/// <summary>Notifies Explorer and running apps that a user setting changed so it applies without sign-out.</summary>
public static class ShellNotify
{
    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

    public static void BroadcastSettingChange(string area)
    {
        try
        {
            SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, UIntPtr.Zero, area, SMTO_ABORTIFHUNG, 1000, out _);
        }
        catch
        {
            // Non-critical: the setting still applies after the next sign-in.
        }
    }
}
