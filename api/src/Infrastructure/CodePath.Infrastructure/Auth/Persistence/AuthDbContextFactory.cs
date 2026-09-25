using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace CodePath.Infrastructure.Auth.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var candidatePaths = new[]
        {
            Path.Combine(currentDir, "..", "..", "..", "Api", "CodePath.Api"),
            Path.Combine(currentDir, "..", "..", "Api", "CodePath.Api"),
            Path.Combine(currentDir, "src", "Api", "CodePath.Api"),
            currentDir
        };

        var apiDir = candidatePaths.FirstOrDefault(Directory.Exists) ?? currentDir;

        var cfg = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(apiDir))
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
