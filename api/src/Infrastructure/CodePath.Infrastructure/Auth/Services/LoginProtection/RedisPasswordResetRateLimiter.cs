using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services.LoginProtection;

public sealed class RedisPasswordResetRateLimiter : IPasswordResetRateLimiter
{
    private const int HourlyQuota = 5;
    private const int CooldownSeconds = 60;
    private readonly IDatabase _redis;

    private static readonly LuaScript AcquireScript = LuaScript.Prepare(@"
        local hourlyQuota = tonumber(@hourlyQuota)
        local cooldownSeconds = tonumber(@cooldownSeconds)

        if redis.call('EXISTS', @cooldownKey) == 1 then
            return 0
        end

        local quota = redis.call('INCR', @quotaKey)
        if quota == 1 then
            redis.call('EXPIRE', @quotaKey, 3600)
        end
        if quota > hourlyQuota then
            return 0
        end

        redis.call('SET', @cooldownKey, '1', 'EX', cooldownSeconds)
        return 1
    ");

    public RedisPasswordResetRateLimiter(IConnectionMultiplexer redis) =>
        _redis = redis.GetDatabase();

    public async Task<bool> TryAcquireAsync(string normalizedEmail)
    {
        var email = normalizedEmail.Trim().ToLowerInvariant();
        var result = await _redis.ScriptEvaluateAsync(AcquireScript, new
        {
            cooldownKey = (RedisKey)$"auth:password-reset:cooldown:{email}",
            quotaKey = (RedisKey)$"auth:password-reset:quota:{email}",
            hourlyQuota = HourlyQuota,
            cooldownSeconds = CooldownSeconds
        });

        return (int)result == 1;
    }
}
