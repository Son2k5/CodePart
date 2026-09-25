using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IDatabase _redis;
    public RedisTokenBlacklistService(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime > TimeSpan.Zero)
            await _redis.StringSetAsync($"auth:blacklist:jti:{jti}", "revoked", remainingLifetime);
    }

    public async Task<bool> IsBlacklistedAsync(string jti) =>
        await _redis.KeyExistsAsync($"auth:blacklist:jti:{jti}");
}
