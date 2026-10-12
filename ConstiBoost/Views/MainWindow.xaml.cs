using System;
using System.ComponentModel;
using System.Windows;
using ConstiBoost.Localization;
using ConstiBoost.Services;
using ConstiBoost.ViewModels;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace ConstiBoost.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) =>
        {
            // Compensate the invisible resize border when maximized with a custom chrome.
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            if (DataContext is MainViewModel vm)
            {
                if (WindowState == WindowState.Minimized)
                {
                    vm.Suspend();
                }
                else
                {
                    vm.Resume();
                }
            }
        };
        Closing += OnClosing;
        SourceInitialized += (_, _) => HotkeyService.Instance.Attach(this);
    }

    private bool _trayHintShown;

    private bool _closeConfirmed;

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeConfirmed && !TrayService.Instance.ExitRequested && SettingsService.Instance.Current.MinimizeToTray)
        {
            // Keep running quietly in the tray (auto profiles, hotkeys, Auto-Cool, overlay keep working).
            e.Cancel = true;
            Hide();
            (DataContext as MainViewModel)?.Suspend();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                TrayService.Instance.ShowBalloon(L.T("ConstiBoost keeps running"), L.T("ConstiBoost is still running in the notification area. Right-click the tray icon → Exit to close it completely."));
            }

            return;
        }

        bool focus = Games.GameFocusService.Instance.IsActive;
        if (_closeConfirmed || (!GamingModeService.Instance.IsActive && !focus))
        {
            return;
        }

        if (!DialogService.Confirm(focus ? "Game Focus is still active" : "Gaming Mode is still active", "Stop it and restore your previous settings before closing? If you keep it, ConstiBoost will offer to stop it the next time it starts.", null, "STOP AND CLOSE"))
        {
            return;
        }

        e.Cancel = true; // restore asynchronously (may need a UAC prompt), then close
        try
        {
            if (focus)
            {
                await Games.GameFocusService.Instance.StopAsync();
            }
            else
            {
                await GamingModeService.Instance.DeactivateAsync(SettingsService.Instance.Current.GamingModeRelaunchClosedApps);
            }
        }
        catch (Exception ex)
        {
            Logging.AppLog.Error("Gaming Mode restore on exit failed", ex);
        }

        _closeConfirmed = true;
        Close();
    }

    private void PageHost_TargetUpdated(object? sender, DataTransferEventArgs e)
    {
        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        PageShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(14, 0, duration) { EasingFunction = ease });
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
