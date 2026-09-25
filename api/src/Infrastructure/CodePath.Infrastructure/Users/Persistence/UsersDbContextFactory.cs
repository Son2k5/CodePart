using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace CodePath.Infrastructure.Users.Persistence;

public sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(conn, o => o.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema))
            .Options;

        return new UsersDbContext(options);
    }
}
