using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Enums;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services;

/// <summary>
/// Adapter Redis cho <see cref="IUserStatusCache"/>. Owns key format + TTL (Infra concern).
/// Key: user:status:{userId} → UserStatus.ToString(), TTL 5 phút.
/// </summary>
public sealed class RedisUserStatusCache : IUserStatusCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    private readonly IDatabase _redis;
    public RedisUserStatusCache(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    private static string KeyFor(Guid userId) => $"user:status:{userId}";

    public async Task<UserStatus?> TryGetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cached = await _redis.StringGetAsync(KeyFor(userId));
        if (!cached.HasValue) return null;
        return Enum.TryParse<UserStatus>(cached.ToString(), out var parsed) ? parsed : null;
    }

    public async Task SetStatusAsync(Guid userId, UserStatus status, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        await _redis.StringSetAsync(KeyFor(userId), status.ToString(), ttl ?? DefaultTtl);
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // StackExchange.Redis chưa hỗ trợ CancellationToken cho KeyDeleteAsync nên bỏ qua token ở đây.
        await _redis.KeyDeleteAsync(KeyFor(userId));
    }
}
