namespace ConstiBoost.Models;

public enum NotificationKind
{
    Info,
    Success,
    Warning,
    Error,
}

public enum CheckStatus
{
    Good,
    Warning,
    Bad,
    Unavailable,
}

public enum ImpactLevel
{
    Low,
    Medium,
    High,
}

public enum PerformanceStatus
{
    Good,
    CouldBeImproved,
    NeedsAttention,
}
