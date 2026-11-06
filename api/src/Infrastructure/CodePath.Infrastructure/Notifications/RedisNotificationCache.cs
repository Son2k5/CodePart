using CodePath.Application.Notifications.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Redis-backed unread notification count cache. All operations are best-effort: on a Redis
/// outage the service transparently falls back to a database count, so failures are swallowed
/// and logged rather than propagated to notification delivery.
/// </summary>
public sealed class RedisNotificationCache : IUnreadCountCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly IDatabase _redis;
    private readonly ILogger<RedisNotificationCache> _logger;

    public RedisNotificationCache(IConnectionMultiplexer redis, ILogger<RedisNotificationCache> logger)
    {
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    private static string Key(Guid userId) => $"notifications:unread:{userId:N}";

    public async Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _redis.StringGetAsync(Key(userId));
            return value.IsNullOrEmpty ? null : (int?)value;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Unread-count cache read failed for {UserId}.", userId);
            return null;
        }
    }

    public async Task SetAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.StringSetAsync(Key(userId), Math.Max(0, count), Ttl);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Unread-count cache set failed for {UserId}.", userId);
        }
    }

    public async Task IncrementAsync(Guid userId, int delta, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only touch the cache when it is already warm; a cold miss is left for the
            // database-backed GetUnreadCountAsync path to repopulate a correct value.
            if (await _redis.KeyExistsAsync(Key(userId)))
                await _redis.StringIncrementAsync(Key(userId), delta);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Unread-count cache increment failed for {UserId}.", userId);
        }
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.KeyDeleteAsync(Key(userId));
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Unread-count cache invalidate failed for {UserId}.", userId);
        }
    }
}
