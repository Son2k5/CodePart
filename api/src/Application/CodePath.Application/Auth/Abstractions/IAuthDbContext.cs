using CodePath.Domain.Auth.Entities;

namespace CodePath.Application.Auth.Abstractions;

/// <summary>
/// Facade persistence cho Auth module. Không expose DbSet/IQueryable
/// để Application không phụ thuộc EF Core (Clean Architecture).
/// </summary>
public interface IAuthDbContext
{
    Task<RefreshToken?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<int> TouchSessionAsync(Guid sessionId, DateTime now, DateTime idleCutoff, DateTime throttleCutoff, CancellationToken cancellationToken = default);
    Task<int> RevokeSessionAsync(Guid sessionId, DateTime now, string reason, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<bool> TryRotateRefreshTokenAsync(string currentTokenHash, RefreshToken replacement, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeTokenFamilyAsync(Guid familyId, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeRefreshTokenAsync(string tokenHash, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeAllUserRefreshTokensAsync(Guid userId, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<bool> RevokeAllUserSessionsAsync(Guid userId, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task ReplaceActivePasswordResetTokenAsync(PasswordResetToken token, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<PasswordResetCompletion?> TryConsumePasswordResetTokenAsync(string tokenHash, string newPasswordHash, DateTime utcNow, string? ipAddress, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredRefreshTokensAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredPasswordResetTokensAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PasswordResetCompletion(Guid UserId, string Email);
