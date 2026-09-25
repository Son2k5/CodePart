using CodePath.Infrastructure.Users.Persistence.Seeders;
using CodePath.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Users.Persistence;

public sealed class UsersDbContextMigrator(
    UsersDbContext dbContext,
    IConfiguration configuration,
    ILogger<UsersDbContextMigrator> logger) : IDatabaseMigrator
{
    public string ModuleName => "Users";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await AdminSeeder.SeedAdminsAsync(dbContext, configuration, logger, cancellationToken);
    }
}
