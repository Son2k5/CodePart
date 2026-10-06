namespace CodePath.Application.Auth.Abstractions;

public interface IPasswordResetRateLimiter
{
    Task<bool> TryAcquireAsync(string normalizedEmail);
}
