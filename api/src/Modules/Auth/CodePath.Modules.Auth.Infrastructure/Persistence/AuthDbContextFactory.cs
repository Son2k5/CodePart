
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
namespace CodePath.Modules.Auth.Infrastructure.Persistence
{
    public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var cfg = new ConfigurationBuilder()
          .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Bootstrapper", "CodePath.Api"))
          .AddJsonFile("appsettings.json", optional: true)
          .AddJsonFile("appsettings.Development.json", optional: true)
          .AddEnvironmentVariables()
          .Build();

            var conn = cfg.GetConnectionString("Database")
                ?? "Host=localhost;Port=5432;Database=codepath;Username=postgres;Password=postgres";

            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseNpgsql(conn, o => o.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema))
                .Options;

            return new AuthDbContext(options);

        }
    }
}