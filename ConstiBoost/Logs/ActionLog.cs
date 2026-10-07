using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using ConstiBoost.Models;

namespace ConstiBoost.Logging;

/// <summary>
/// Human readable change log. Every system change ConstiBoost makes is written here:
/// timestamp, action, old value, new value and result.
/// </summary>
public static class ActionLog
{
    private static readonly object Gate = new();

    public static ObservableCollection<ActionLogEntry> Entries { get; } = new();

    public static string CurrentFile =>
        Path.Combine(Core.AppPaths.Logs, $"changes-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Success(string action, string? oldValue, string? newValue) =>
        Add(new ActionLogEntry(DateTime.Now, action, oldValue, newValue, true, null));

    public static void Failed(string action, string? oldValue, string? newValue, string reason) =>
        Add(new ActionLogEntry(DateTime.Now, action, oldValue, newValue, false, reason));

    public static void Add(ActionLogEntry entry)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]");
            sb.AppendLine(entry.Action);
            if (entry.OldValue != null || entry.NewValue != null)
            {
                sb.AppendLine($"{entry.OldValue ?? "-"} → {entry.NewValue ?? "-"}");
            }

            sb.AppendLine(entry.Success ? "SUCCESS" : "FAILED");
            if (!entry.Success && !string.IsNullOrWhiteSpace(entry.Reason))
            {
                sb.AppendLine("Reason: " + entry.Reason);
            }

            sb.AppendLine();
            lock (Gate)
            {
                File.AppendAllText(CurrentFile, sb.ToString());
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not write change log: " + ex.Message);
        }

        void AddToUi()
        {
            Entries.Insert(0, entry);
            while (Entries.Count > 500)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AddToUi();
        }
        else
        {
            dispatcher.BeginInvoke(AddToUi);
        }
    }
}
