namespace CodePath.Application.Auth.Abstractions;

/// <summary>
/// Probe sức khỏe Redis trừu tượng để Api/Health check không phụ thuộc StackExchange.Redis.
/// Implement ở Infrastructure (owns StackExchange types).
/// </summary>
public sealed record RedisHealthResult(
    bool IsConnected,
    bool RoundTripSuccess,
    long LatencyMs,
    string? Endpoint,
    string? Error);

public interface IRedisHealthProbe
{
    Task<RedisHealthResult> CheckAsync(CancellationToken cancellationToken = default);
}
