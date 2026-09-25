using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Infrastructure.Auth.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Auth.Persistence;

public class AuthDbContext : DbContext, IAuthDbContext
{
    public const string Schema = "auth";

    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema(Schema);
        m.ApplyConfiguration(new RefreshTokenConfiguration());
        base.OnModelCreating(m);
    }
}
