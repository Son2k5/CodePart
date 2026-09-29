using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodePath.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAuthDbContext, IUsersDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Auth Module Entities
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Users Module Entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    // IAuthDbContext Implementation
    Task<RefreshToken?> IAuthDbContext.GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
        => RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    Task<RefreshToken?> IAuthDbContext.GetActiveRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
        => RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.RevokedAt.HasValue && t.ExpiresAt > DateTime.UtcNow, cancellationToken);

    Task<List<RefreshToken>> IAuthDbContext.GetActiveUserTokensAsync(Guid userId, CancellationToken cancellationToken)
        => RefreshTokens.Where(t => t.UserId == userId && !t.RevokedAt.HasValue && t.ExpiresAt > DateTime.UtcNow).ToListAsync(cancellationToken);

    Task IAuthDbContext.AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
        => RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    Task<RefreshToken?> IAuthDbContext.GetUserRefreshTokenAsync(string tokenHash, Guid userId, CancellationToken cancellationToken)
        => RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UserId == userId, cancellationToken);

    // IUsersDbContext Implementation
    Task<User?> IUsersDbContext.GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    Task<User?> IUsersDbContext.GetByEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    Task<User?> IUsersDbContext.GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    Task<User?> IUsersDbContext.GetByIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken)
        => Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    Task IUsersDbContext.AddAsync(User user, CancellationToken cancellationToken)
        => Users.AddAsync(user, cancellationToken).AsTask();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("Email, Student ID, or unique record already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
