namespace ZenBox.Models;

public enum ErrorCategory
{
    NetworkOffline,
    WebLoadFailed,
    MediaFileCorrupted,
    UnexpectedSystemError
}

public sealed class VietnameseErrorInfo
{
    public required ErrorCategory Category { get; init; }
    public required string Title { get; init; }
    public required string Explanation { get; init; }
    public required IReadOnlyList<string> RecoverySteps { get; init; }
    public Action? RetryAction { get; init; }
    public bool CanRetry => RetryAction is not null;
}
