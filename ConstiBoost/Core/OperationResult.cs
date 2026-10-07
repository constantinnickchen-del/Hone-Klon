namespace ConstiBoost.Core;

public sealed record OperationResult(bool Success, string Message, string? TechnicalDetails = null)
{
    public static OperationResult Ok(string message = "Done") => new(true, message);

    public static OperationResult Fail(string message, string? details = null) => new(false, message, details);

    public static OperationResult Unavailable(string reason) =>
        new(false, "This optimization is unavailable on your system. " + reason);
}
