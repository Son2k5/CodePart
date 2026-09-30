namespace CodePath.Application.Auth.Abstractions;

public sealed record LoginFailureResult(bool IsLocked, TimeSpan? RetryAfter);

public interface ILoginAttemptService
{
    Task<TimeSpan?> GetLockoutRemainingAsync(string normalizedEmail);
    Task<LoginFailureResult> RecordFailureAsync(string normalizedEmail);
    Task ResetAsync(string normalizedEmail);
}
