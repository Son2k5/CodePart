using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Health;

public sealed class RedisDependencyHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDependencyHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy(
                "Redis is reachable.",
                new Dictionary<string, object>
                {
                    ["latencyMs"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds
                });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", exception);
        }
    }
}
