using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Redis;

/// <summary>
/// Implement probe sức khỏe Redis. Infra owns StackExchange types,
/// Api chỉ phụ thuộc <see cref="IRedisHealthProbe"/>.
/// </summary>
public sealed class RedisHealthProbe : IRedisHealthProbe
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthProbe(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<RedisHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
                return new RedisHealthResult(false, false, 0, null, "Cannot connect to Redis server.");

            var db = _redis.GetDatabase();
            var testKey = "codepath:healthcheck";
            var testValue = $"ping_{Guid.NewGuid():N}";

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(60));
            var retrievedValue = await db.StringGetAsync(testKey);
            stopwatch.Stop();
            await db.KeyDeleteAsync(testKey);

            var ok = retrievedValue == testValue;
            return new RedisHealthResult(
                true,
                ok,
                stopwatch.ElapsedMilliseconds,
                _redis.GetEndPoints().FirstOrDefault()?.ToString(),
                ok ? null : "Redis round-trip verification failed (value mismatch).");
        }
        catch (Exception ex)
        {
            return new RedisHealthResult(false, false, 0, null, ex.Message);
        }
    }
}
