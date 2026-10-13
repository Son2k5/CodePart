using CodePath.Application.Auth;
using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Persistence.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAdminsAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        AdminSeedOptions seedOptions,
        ILogger logger,
        TimeProvider timeProvider,
        CancellationToken ct = default)
    {
        if (!seedOptions.IsConfigured)
        {
            logger.LogWarning("There are some problems with service");
            return;
        }

        if (!PasswordPolicy.IsValid(seedOptions.Admin01Password))
        {
            throw new InvalidOperationException();
        }

        if (!PasswordPolicy.IsValid(seedOptions.Admin02Password))
        {
            throw new InvalidOperationException();
        }

        var adminsToSeed = new[]
        {
            new
            {
                Email = seedOptions.Admin01Email,
                Password = seedOptions.Admin01Password!,
                FullName = seedOptions.Admin01FullName
            },
            new
            {
                Email = seedOptions.Admin02Email,
                Password = seedOptions.Admin02Password!,
                FullName = seedOptions.Admin02FullName
            }
        };

        foreach (var admin in adminsToSeed)
        {
            var exists = await dbContext.Users.AnyAsync(u => u.Email == admin.Email, ct);
            if (!exists)
            {
                var hash = passwordHasher.HashPassword(admin.Password);
                var adminUser = User.CreateAdmin(
                    admin.FullName,
                    admin.Email,
                    hash,
                    timeProvider.GetUtcNow().UtcDateTime);
                await dbContext.Users.AddAsync(adminUser, ct);
                logger.LogInformation("Seeded admin account: {Email}", admin.Email);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
