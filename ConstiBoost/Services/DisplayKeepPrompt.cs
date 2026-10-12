using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ConstiBoost.Views;

namespace ConstiBoost.Services;

/// <summary>"Keep this display mode?" – reverts automatically when not confirmed within 15 seconds (like Windows does).</summary>
public static class DisplayKeepPrompt
{
    public static Task<bool> AskAsync(string title)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.FromResult(true);
        }

        return dispatcher.InvokeAsync(() =>
        {
            var dialog = new ConfirmDialog(title, "Does the picture look right? Without confirmation the previous mode returns in 15 seconds.",
                new List<string> { "KEEP = use the new mode", "Cancel or wait = go back" }, "KEEP", false, showCancel: true);
            int left = 15;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                left--;
                dialog.MessageText.Text = $"Does the picture look right? Without confirmation the previous mode returns in {left} s.";
                if (left <= 0)
                {
                    timer.Stop();
                    dialog.DialogResult = false;
                }
            };
            timer.Start();
            bool keep = dialog.ShowDialog() == true;
            timer.Stop();
            return keep;
        }).Task;
    }
}
