using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using ConstiBoost.Games;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using WinForms = System.Windows.Forms;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>Notification-area icon with quick actions. ConstiBoost keeps running quietly in the tray.</summary>
public sealed class TrayService : IDisposable
{
    private WinForms.NotifyIcon? _icon;
    private WinForms.ToolStripMenuItem? _gamingItem;
    private WinForms.ToolStripMenuItem? _overlayItem;
    private WinForms.ToolStripMenuItem? _focusItem;

    public static TrayService Instance { get; } = new();

    public bool ExitRequested { get; private set; }

    public void Initialize()
    {
        try
        {
            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add(L.T("Open ConstiBoost"), null, (_, _) => ShowWindow());
            _gamingItem = new WinForms.ToolStripMenuItem(L.T("Start Gaming Mode  (Ctrl+Alt+G)"), null, async (_, _) => await QuickActions.ToggleGamingModeAsync());
            _focusItem = new WinForms.ToolStripMenuItem(L.T("Stop Game Focus"), null, async (_, _) => await GameFocusService.Instance.StopAsync());
            _overlayItem = new WinForms.ToolStripMenuItem(L.T("Show FPS overlay  (Ctrl+Alt+O)"), null, (_, _) => OverlayService.Instance.Toggle());
            menu.Items.Add(_gamingItem);
            menu.Items.Add(_focusItem);
            menu.Items.Add(_overlayItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(L.T("Exit ConstiBoost"), null, (_, _) => Exit());
            menu.Opening += (_, _) => UpdateMenu();

            Icon? icon = null;
            try
            {
                icon = Icon.ExtractAssociatedIcon(Core.AppPaths.ExecutablePath);
            }
            catch
            {
                // default icon
            }

            _icon = new WinForms.NotifyIcon
            {
                Icon = icon ?? SystemIcons.Application,
                Text = "ConstiBoost PRO",
                ContextMenuStrip = menu,
                Visible = true,
            };
            _icon.DoubleClick += (_, _) => ShowWindow();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Tray icon unavailable: " + ex.Message);
        }
    }

    private void UpdateMenu()
    {
        if (_gamingItem != null)
        {
            _gamingItem.Text = GamingModeService.Instance.IsActive ? L.T("Stop Gaming Mode  (Ctrl+Alt+G)") : L.T("Start Gaming Mode  (Ctrl+Alt+G)");
        }

        if (_focusItem != null)
        {
            _focusItem.Visible = GameFocusService.Instance.IsActive;
            _focusItem.Text = GameFocusService.Instance.State is { } s ? L.F("Stop Game Focus ({0})", s.GameName) : L.T("Stop Game Focus");
        }

        if (_overlayItem != null)
        {
            _overlayItem.Text = OverlayService.Instance.IsVisible ? L.T("Hide FPS overlay  (Ctrl+Alt+O)") : L.T("Show FPS overlay  (Ctrl+Alt+O)");
        }
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            var window = Application.Current?.MainWindow;
            if (_icon != null && (window is null || !window.IsVisible || window.WindowState == WindowState.Minimized))
            {
                _icon.ShowBalloonTip(5000, title, text.Length > 250 ? text[..250] + "…" : text, WinForms.ToolTipIcon.Info);
            }
        }
        catch
        {
            // ignore
        }
    }

    public static void ShowWindow()
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        ViewModels.MainViewModel.Instance?.Resume();
    }

    public void Exit()
    {
        ExitRequested = true;
        Application.Current?.MainWindow?.Close();
    }

    public void Dispose()
    {
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}

/// <summary>Actions for tray menu and hotkeys (no dialogs – a hotkey must work while a game is in front).</summary>
public static class QuickActions
{
    public static async System.Threading.Tasks.Task ToggleGamingModeAsync()
    {
        try
        {
            if (GamingModeService.Instance.IsActive)
            {
                await GamingModeService.Instance.DeactivateAsync(SettingsService.Instance.Current.GamingModeRelaunchClosedApps);
                Tell(L.T("Gaming Mode stopped – settings restored."));
                return;
            }

            var apps = BackgroundAppsService.GetCandidates()
                .Where(a => a.IsKnownBackgroundApp && !BackgroundAppsService.KeepPriority.Contains(a.ProcessName) &&
                            !BackgroundAppsService.BrowserProcesses.Contains(a.ProcessName) &&
                            !a.ProcessName.StartsWith("steam", StringComparison.OrdinalIgnoreCase) && !a.ProcessName.StartsWith("EpicGames", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var profile = SettingsService.Instance.Current.GamingProfile;
            var messages = await GamingModeService.Instance.ActivateAsync(apps, new GamingModeOptions(profile, true, null));
            Tell(L.F("Gaming Mode ({0}) active", L.T(GamingModeService.ProfileName(profile))) + (apps.Count > 0 ? L.F(" – closed {0}", string.Join(", ", apps.Select(a => a.DisplayName))) : string.Empty) +
                 (messages.Count > 0 ? L.T(". Notes: ") + string.Join("; ", messages) : "."));
        }
        catch (Exception ex)
        {
            AppLog.Error("Quick gaming mode failed", ex);
        }
    }

    private static void Tell(string message)
    {
        NotificationService.Instance.Show(message, NotificationKind.Info);
        TrayService.Instance.ShowBalloon("ConstiBoost", message);
        OverlayService.Instance.Flash(message);
    }
}
