using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ConstiBoost.WinSys;

public sealed record DisplayMode(int Width, int Height, int RefreshRate)
{
    public override string ToString() => $"{Width}×{Height} @ {RefreshRate} Hz";
}

/// <summary>Primary display modes through the documented EnumDisplaySettings / ChangeDisplaySettingsEx API.</summary>
public static class DisplayModes
{
    private const int EnumCurrentSettings = -1;
    private const int DmPelsWidth = 0x80000;
    private const int DmPelsHeight = 0x100000;
    private const int DmDisplayFrequency = 0x400000;
    private const int CdsUpdateRegistry = 0x01;
    private const int CdsTest = 0x02;
    private const int DispChangeSuccessful = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string? deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChangeDisplaySettingsEx")]
    private static extern int ChangeDisplaySettingsExReset(string? deviceName, IntPtr devMode, IntPtr hwnd, int flags, IntPtr param);

    private static DEVMODE NewMode() => new() { dmSize = (short)Marshal.SizeOf<DEVMODE>(), dmDeviceName = string.Empty, dmFormName = string.Empty };

    public static DisplayMode? Current()
    {
        var mode = NewMode();
        return EnumDisplaySettings(null, EnumCurrentSettings, ref mode) ? new DisplayMode(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency) : null;
    }

    public static List<DisplayMode> All()
    {
        var list = new HashSet<DisplayMode>();
        var mode = NewMode();
        for (int i = 0; EnumDisplaySettings(null, i, ref mode); i++)
        {
            if (mode.dmBitsPerPel >= 32 && mode.dmPelsWidth >= 800)
            {
                list.Add(new DisplayMode(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency));
            }

            mode = NewMode();
        }

        return list.OrderByDescending(m => m.Width * m.Height).ThenByDescending(m => m.RefreshRate).ToList();
    }

    /// <param name="persist">true = saved for the next sign-in, false = only until restart/sign-out (or Undo).</param>
    public static void Apply(DisplayMode target, bool persist)
    {
        var mode = NewMode();
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
        {
            throw new InvalidOperationException("The current display mode could not be read.");
        }

        mode.dmPelsWidth = target.Width;
        mode.dmPelsHeight = target.Height;
        mode.dmDisplayFrequency = target.RefreshRate;
        mode.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
        int test = ChangeDisplaySettingsEx(null, ref mode, IntPtr.Zero, CdsTest, IntPtr.Zero);
        if (test != DispChangeSuccessful)
        {
            throw new InvalidOperationException($"The display does not accept {target} (code {test}).");
        }

        int result = ChangeDisplaySettingsEx(null, ref mode, IntPtr.Zero, persist ? CdsUpdateRegistry : 0, IntPtr.Zero);
        if (result != DispChangeSuccessful)
        {
            throw new InvalidOperationException($"Display mode change failed (code {result}).");
        }
    }

    /// <summary>Returns to the mode saved in Windows settings (undoes a temporary change).</summary>
    public static void ResetToSaved() => ChangeDisplaySettingsExReset(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
}
