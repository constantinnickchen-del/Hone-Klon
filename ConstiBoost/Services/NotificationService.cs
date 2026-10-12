using System;
using System.Windows;
using ConstiBoost.Models;

namespace ConstiBoost.Services;

/// <summary>In-app toast notifications. Errors and warnings are always shown.</summary>
public sealed class NotificationService
{
    public static NotificationService Instance { get; } = new();

    public event Action<Notification>? Raised;

    public void Show(string message, NotificationKind kind = NotificationKind.Info)
    {
        message = Localization.L.T(message);
        if (!SettingsService.Instance.Current.Notifications && kind is NotificationKind.Info or NotificationKind.Success)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            Raised?.Invoke(new Notification(message, kind));
        }
        else
        {
            dispatcher.BeginInvoke(() => Raised?.Invoke(new Notification(message, kind)));
        }
    }
}
