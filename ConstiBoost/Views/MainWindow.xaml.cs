using System;
using System.ComponentModel;
using System.Windows;
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
    }

    private bool _closeConfirmed;

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed || !GamingModeService.Instance.IsActive)
        {
            return;
        }

        if (!DialogService.Confirm("Gaming Mode is still active", "Stop Gaming Mode and restore your previous settings before closing? If you keep it, ConstiBoost will offer to stop it the next time it starts.", null, "STOP AND CLOSE"))
        {
            return;
        }

        e.Cancel = true; // restore asynchronously (may need a UAC prompt), then close
        try
        {
            await GamingModeService.Instance.DeactivateAsync(SettingsService.Instance.Current.GamingModeRelaunchClosedApps);
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
