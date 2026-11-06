namespace CodePath.Application.Auth.Abstractions;

public interface IPasswordResetRateLimiter
{
    /// <summary>Coarse throttling on both the target email and the requesting IP.</summary>
    Task<bool> TryAcquireAsync(string normalizedEmail, string? ipAddress);
}
