using CodePath.Domain.Auth.Entities;

namespace CodePath.Application.Auth.Abstractions;

/// <summary>
/// Facade persistence cho Auth module. Không expose DbSet/IQueryable
/// để Application không phụ thuộc EF Core (Clean Architecture).
/// </summary>
public interface IAuthDbContext
{
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetActiveUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetUserRefreshTokenAsync(string tokenHash, Guid userId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
