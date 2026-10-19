using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Auth.Entities;

namespace CodePath.Application.Auth.Services;

public sealed class SessionActivityService(IAuthDbContext db, IAuthSessionPolicy policy, TimeProvider time)
{
    public const string IdleCode = "SESSION_IDLE_TIMEOUT";
    public const string ExpiredCode = "SESSION_EXPIRED";

    public static string? Check(RefreshToken token, DateTime now, TimeSpan idleTimeout)
    {
        if (now >= token.AbsoluteExpiresAt || now >= token.ExpiresAt) return ExpiredCode;
        if (token.IsRevoked || now - token.LastActiveAt >= idleTimeout) return IdleCode;
        return null;
    }

    public async Task<string?> ValidateAsync(Guid sessionId, Guid userId, bool trackActivity, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var token = await db.GetSessionAsync(sessionId, ct);
        if (token is null || token.UserId != userId) return IdleCode;
        var error = Check(token, now, policy.IdleTimeout);
        if (error is not null)
        {
            await db.RevokeSessionAsync(sessionId, now, error == IdleCode ? "IDLE_TIMEOUT" : "SESSION_EXPIRED", ct);
            return error;
        }
        if (trackActivity && now - token.LastActiveAt >= policy.ActivityUpdateThrottle)
        {
            await db.TouchSessionAsync(sessionId, now, now - policy.IdleTimeout, now - policy.ActivityUpdateThrottle, ct);
            // Re-read: a concurrent logout or rotation must not resurrect the session.
            token = await db.GetSessionAsync(sessionId, ct);
            return token is null ? IdleCode : Check(token, now, policy.IdleTimeout);
        }
        return null;
    }
}
