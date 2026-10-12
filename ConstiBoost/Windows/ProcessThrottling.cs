using System;
using System.Runtime.InteropServices;

namespace ConstiBoost.WinSys;

/// <summary>
/// Windows "EcoQoS" / efficiency mode per process (SetProcessInformation, ProcessPowerThrottling).
/// Opting a game out keeps Windows from running it on efficiency clocks; it only lasts as long as the process runs.
/// </summary>
public static class ProcessThrottling
{
    private const int ProcessPowerThrottling = 4;
    private const uint ExecutionSpeed = 0x1;
    private const uint ProcessSetInformation = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <param name="disableThrottling">true = never throttle (high performance), false = let Windows decide again.</param>
    public static bool Set(int pid, bool disableThrottling)
    {
        IntPtr handle = OpenProcess(ProcessSetInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = 1,
                ControlMask = disableThrottling ? ExecutionSpeed : 0,
                StateMask = 0,
            };
            return SetProcessInformation(handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
