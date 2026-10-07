using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ConstiBoost.Views;

namespace ConstiBoost.Services;

public static class DialogService
{
    public static bool Confirm(string title, string message, IEnumerable<string>? details = null, string confirmText = "CONFIRM", bool danger = false)
    {
        var dialog = new ConfirmDialog(title, message, details?.ToList() ?? new List<string>(), confirmText, danger, showCancel: true)
        {
            Owner = Application.Current?.MainWindow,
        };
        return dialog.ShowDialog() == true;
    }

    public static void Info(string title, string message, IEnumerable<string>? details = null)
    {
        var dialog = new ConfirmDialog(title, message, details?.ToList() ?? new List<string>(), "OK", false, showCancel: false)
        {
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }
}
