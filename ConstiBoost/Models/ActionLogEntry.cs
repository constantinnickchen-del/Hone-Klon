using System;

namespace ConstiBoost.Models;

public sealed record ActionLogEntry(DateTime Timestamp, string Action, string? OldValue, string? NewValue, bool Success, string? Reason)
{
    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public string ChangeText => OldValue is null && NewValue is null ? string.Empty : $"{OldValue ?? "-"} → {NewValue ?? "-"}";

    public string ResultText => Success ? "SUCCESS" : "FAILED";
}
