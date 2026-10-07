using System;
using System.Collections.Generic;

namespace ConstiBoost.WinSys;

public sealed record WindowState(bool HasVisibleWindow, bool IsHung);

/// <summary>Enumerates top-level windows once and maps them to process IDs (much cheaper than Process.MainWindowHandle per process).</summary>
public static class WindowEnumerator
{
    public static Dictionary<int, WindowState> Snapshot()
    {
        var result = new Dictionary<int, WindowState>();
        try
        {
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(hwnd) ||
                    NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero ||
                    NativeMethods.GetWindowTextLength(hwnd) == 0)
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);
                bool hung = NativeMethods.IsHungAppWindow(hwnd);
                if (result.TryGetValue(pid, out var existing))
                {
                    result[pid] = new WindowState(true, existing.IsHung || hung);
                }
                else
                {
                    result[pid] = new WindowState(true, hung);
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Window information is optional.
        }

        return result;
    }
}
