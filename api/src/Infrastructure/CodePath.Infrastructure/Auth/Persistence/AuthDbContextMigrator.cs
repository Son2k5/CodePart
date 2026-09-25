using CodePath.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Auth.Persistence;

public sealed class AuthDbContextMigrator(AuthDbContext dbContext) : IDatabaseMigrator
{
    public string ModuleName => "Auth";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
