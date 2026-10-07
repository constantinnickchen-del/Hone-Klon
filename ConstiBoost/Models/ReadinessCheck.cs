namespace ConstiBoost.Models;

public sealed record ReadinessCheck(string Text, CheckStatus Status, int Points, int MaxPoints)
{
    public string Symbol => Status switch
    {
        CheckStatus.Good => "✓",
        CheckStatus.Warning => "⚠",
        CheckStatus.Bad => "✕",
        _ => "–",
    };
}
