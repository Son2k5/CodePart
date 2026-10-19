using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext
{
    Task<RefreshToken?> IAuthDbContext.GetSessionAsync(Guid sessionId, CancellationToken ct)
        => RefreshTokens.AsNoTracking().Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.RevokedAt != null).ThenByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);

    Task<int> IAuthDbContext.TouchSessionAsync(Guid sessionId, DateTime now, DateTime idleCutoff, DateTime throttleCutoff, CancellationToken ct)
        => RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedAt == null
                && t.ExpiresAt > now && t.AbsoluteExpiresAt > now
                && t.LastActiveAt > idleCutoff && t.LastActiveAt <= throttleCutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastActiveAt, now), ct);

    Task<int> IAuthDbContext.RevokeSessionAsync(Guid sessionId, DateTime now, string reason, CancellationToken ct)
        => RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason), ct);
}
