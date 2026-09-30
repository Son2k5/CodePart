using CodePath.Domain.Auth.Entities;

namespace CodePath.Application.Auth.Abstractions;

/// <summary>
/// Facade persistence cho Auth module. Không expose DbSet/IQueryable
/// để Application không phụ thuộc EF Core (Clean Architecture).
/// </summary>
public interface IAuthDbContext
{
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<bool> TryRotateRefreshTokenAsync(string currentTokenHash, RefreshToken replacement, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeTokenFamilyAsync(Guid familyId, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeRefreshTokenAsync(string tokenHash, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> RevokeAllUserRefreshTokensAsync(Guid userId, DateTime revokedAtUtc, string? revokedByIp, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredRefreshTokensAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
