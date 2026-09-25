using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Application.Users.Abstractions;

public interface IUsersDbContext
{
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
