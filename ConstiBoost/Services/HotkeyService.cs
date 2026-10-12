using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ConstiBoost.Logging;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>Global hotkeys via the documented RegisterHotKey API (work while a game is in the foreground).</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int IdGaming = 0xC001;
    private const int IdOverlay = 0xC002;
    private const int IdWindow = 0xC003;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;

    public static HotkeyService Instance { get; } = new();

    public string Status { get; private set; } = L.T("Off");

    public void Attach(Window window)
    {
        if (!SettingsService.Instance.Current.HotkeysEnabled)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(Hook);
        bool g = RegisterHotKey(handle, IdGaming, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x47); // G
        bool o = RegisterHotKey(handle, IdOverlay, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x4F); // O
        bool w = RegisterHotKey(handle, IdWindow, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x43); // C
        Status = g && o && w ? L.T("Ctrl+Alt+G Gaming Mode · Ctrl+Alt+O Overlay · Ctrl+Alt+C ConstiBoost") : L.T("Some hotkeys are used by another program");
        if (!(g && o && w))
        {
            AppLog.Warn("Hotkeys partly unavailable (already registered by another app).");
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case IdGaming:
                _ = QuickActions.ToggleGamingModeAsync();
                handled = true;
                break;
            case IdOverlay:
                OverlayService.Instance.Toggle();
                handled = true;
                break;
            case IdWindow:
                var window = Application.Current?.MainWindow;
                if (window != null && window.IsVisible && window.WindowState != WindowState.Minimized && window.IsActive)
                {
                    window.Hide();
                }
                else
                {
                    TrayService.ShowWindow();
                }

                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, IdGaming);
            UnregisterHotKey(_source.Handle, IdOverlay);
            UnregisterHotKey(_source.Handle, IdWindow);
            _source.RemoveHook(Hook);
            _source = null;
        }
    }
}
