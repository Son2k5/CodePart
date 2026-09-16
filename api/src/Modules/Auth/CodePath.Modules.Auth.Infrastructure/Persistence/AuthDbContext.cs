using CodePath.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Modules.Auth.Infrastructure.Persistence
{
    public class AuthDbContext : DbContext
    {
        public const string Schema = "auth";

        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.HasDefaultSchema(Schema);
            m.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
            base.OnModelCreating(m);
        }
    }
}