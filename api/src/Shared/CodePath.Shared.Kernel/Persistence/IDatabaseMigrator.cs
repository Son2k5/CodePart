namespace CodePath.Shared.Kernel.Persistence;

public interface IDatabaseMigrator
{
    string ModuleName { get; }
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
