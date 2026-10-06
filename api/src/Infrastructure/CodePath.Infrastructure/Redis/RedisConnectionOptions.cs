namespace CodePath.Infrastructure.Redis;

public sealed class RedisConnectionOptions
{
    public const string SectionName = "Infrastructure:Redis";

    public int ConnectRetry { get; init; } = 3;
    public int ConnectTimeoutMilliseconds { get; init; } = 5000;
    public int SyncTimeoutMilliseconds { get; init; } = 5000;
    public int KeepAliveSeconds { get; init; } = 60;
}
