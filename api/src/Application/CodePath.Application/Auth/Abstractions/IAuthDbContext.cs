using CodePath.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Application.Auth.Abstractions;

public interface IAuthDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
