namespace CodePath.Infrastructure.Persistence;

public sealed class DatabasePoolOptions
{
    public const string SectionName = "Infrastructure:MySql";

    public int? MinPoolSize { get; init; }
    public int? MaxPoolSize { get; init; }
    public int? ConnectionIdleLifetimeSeconds { get; init; }
}
