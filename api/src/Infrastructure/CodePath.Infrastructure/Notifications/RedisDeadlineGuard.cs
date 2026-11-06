using CodePath.Application.Notifications.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Redis-backed <see cref="IDeadlineGuard"/>. A key is claimed with <c>SET NX EX</c> so a given
/// deadline threshold is emitted at most once per student/topic. Mirrors the best-effort style of
/// <see cref="RedisNotificationCache"/>: on a Redis outage the guard fails open (allows the send)
/// so a deadline alert is never silently dropped.
/// </summary>
public sealed class RedisDeadlineGuard : IDeadlineGuard
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisDeadlineGuard> _logger;

    public RedisDeadlineGuard(IConnectionMultiplexer redis, ILogger<RedisDeadlineGuard> logger)
    {
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> TryAcquireOnceAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redis.StringSetAsync(key, "1", ttl, When.NotExists);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Deadline guard unavailable for {Key}; sending without dedup guard.", key);
            return true; // Fail open so a deadline alert is not dropped.
        }
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Deadline guard release failed for {Key}; it expires on its own.", key);
        }
    }
}
