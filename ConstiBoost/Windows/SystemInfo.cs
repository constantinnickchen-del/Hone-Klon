using System;
using System.Security.Principal;

namespace ConstiBoost.WinSys;

public static class SystemInfo
{
    public static bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public static (ulong total, ulong available, uint loadPercent)? GetMemoryStatus()
    {
        try
        {
            var status = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(status))
            {
                return (status.ullTotalPhys, status.ullAvailPhys, status.dwMemoryLoad);
            }
        }
        catch
        {
            // Unavailable.
        }

        return null;
    }

    public static (bool? onAc, int? batteryPercent, bool hasBattery) GetPowerStatus()
    {
        try
        {
            if (NativeMethods.GetSystemPowerStatus(out var s))
            {
                bool? ac = s.ACLineStatus switch { 0 => false, 1 => true, _ => null };
                bool hasBattery = (s.BatteryFlag & 128) == 0 && s.BatteryFlag != 255;
                int? pct = s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : null;
                return (ac, hasBattery ? pct : null, hasBattery);
            }
        }
        catch
        {
            // Unavailable.
        }

        return (null, null, false);
    }

    public static int WindowsBuild => Environment.OSVersion.Version.Build;
}
