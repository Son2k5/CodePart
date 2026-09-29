using CodePath.Domain.Users.Entities;

namespace CodePath.Application.Users.Abstractions;

/// <summary>
/// Facade persistence cho Users module. Không expose DbSet/IQueryable
/// để Application không phụ thuộc EF Core (Clean Architecture).
/// </summary>
public interface IUsersDbContext
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetByIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
