using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Persistence;
using CodePath.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Api.Extensions;

/// <summary>
/// Tự động chạy migration và seed dữ liệu khi API khởi động.
/// </summary>
public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            logger.LogInformation("Applying database migrations for AppDbContext...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");

            var config = services.GetRequiredService<IConfiguration>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher>();
            var seedOptions = AdminSeedOptions.FromConfiguration(config);

            await AdminSeeder.SeedAdminsAsync(dbContext, passwordHasher, seedOptions, logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration or seeding failed: {Error}", ex.Message);
            throw;
        }
    }
}
