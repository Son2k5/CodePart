using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Users.Persistence.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAdminsAsync(
        UsersDbContext dbContext,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct = default)
    {
        var admin01Password = config["ADMIN01_PASSWORD"] ?? config["AdminSeed:Admin01:Password"];
        var admin02Password = config["ADMIN02_PASSWORD"] ?? config["AdminSeed:Admin02:Password"];

        if (string.IsNullOrWhiteSpace(admin01Password) || string.IsNullOrWhiteSpace(admin02Password))
        {
            logger.LogWarning("Mật khẩu Admin Seed (ADMIN01_PASSWORD / ADMIN02_PASSWORD) chưa được cấu hình. Bỏ qua seeding để bảo đảm an toàn hệ thống.");
            return;
        }

        var adminsToSeed = new[]
        {
            new
            {
                Email = (config["ADMIN01_EMAIL"] ?? config["AdminSeed:Admin01:Email"] ?? "admin01@hanu.edu.vn").Trim().ToLowerInvariant(),
                Password = admin01Password,
                FullName = config["ADMIN01_FULLNAME"] ?? config["AdminSeed:Admin01:FullName"] ?? "System Administrator 01"
            },
            new
            {
                Email = (config["ADMIN02_EMAIL"] ?? config["AdminSeed:Admin02:Email"] ?? "admin02@hanu.edu.vn").Trim().ToLowerInvariant(),
                Password = admin02Password,
                FullName = config["ADMIN02_FULLNAME"] ?? config["AdminSeed:Admin02:FullName"] ?? "System Administrator 02"
            }
        };

        foreach (var admin in adminsToSeed)
        {
            var exists = await dbContext.Users.AnyAsync(u => u.Email == admin.Email, ct);
            if (!exists)
            {
                var hash = BCrypt.Net.BCrypt.EnhancedHashPassword(admin.Password, workFactor: 12);
                var adminUser = User.CreateAdmin(admin.FullName, admin.Email, hash);
                await dbContext.Users.AddAsync(adminUser, ct);
                logger.LogInformation("Seeded admin account: {Email}", admin.Email);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
