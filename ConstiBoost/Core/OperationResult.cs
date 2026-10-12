using ConstiBoost.Localization;

namespace ConstiBoost.Core;

public sealed record OperationResult(bool Success, string Message, string? TechnicalDetails = null)
{
    public static OperationResult Ok(string message = "Done") => new(true, L.T(message));

    public static OperationResult Fail(string message, string? details = null) => new(false, message, details);

    public static OperationResult Unavailable(string reason) =>
        new(false, L.T("This optimization is unavailable on your system.") + " " + L.T(reason));
}
