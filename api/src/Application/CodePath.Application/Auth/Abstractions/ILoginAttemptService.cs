namespace CodePath.Application.Auth.Abstractions;

public sealed record LoginAttemptDecision(TimeSpan? RetryAfter)
{
    public bool IsInBackoff => RetryAfter is { } value && value > TimeSpan.Zero;
}

public interface ILoginAttemptService
{
    Task<LoginAttemptDecision> GetDecisionAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<LoginAttemptDecision> RecordFailureAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task ResetAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
