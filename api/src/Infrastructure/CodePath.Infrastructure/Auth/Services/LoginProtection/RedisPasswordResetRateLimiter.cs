using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services.LoginProtection;

public sealed class RedisPasswordResetRateLimiter : IPasswordResetRateLimiter
{
    private const int HourlyQuota = 5;
    private const int CooldownSeconds = 60;
    private const int IpHourlyQuota = 20;
    private const int IpCooldownSeconds = 30;
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

    public async Task<bool> TryAcquireAsync(string normalizedEmail, string? ipAddress)
    {
        var email = normalizedEmail.Trim().ToLowerInvariant();
        var emailAllowed = await TryAcquireCoreAsync(
            $"auth:password-reset:cooldown:{email}",
            $"auth:password-reset:quota:{email}",
            HourlyQuota,
            CooldownSeconds);
        if (!emailAllowed)
            return false;

        if (string.IsNullOrWhiteSpace(ipAddress))
            return true;

        // IP dimension: a burst of resets against different emails from one IP is still throttled,
        // but an IP miss must not undo the email cooldown already taken.
        var ipAllowed = await TryAcquireCoreAsync(
            $"auth:password-reset:cooldown-ip:{ipAddress}",
            $"auth:password-reset:quota-ip:{ipAddress}",
            IpHourlyQuota,
            IpCooldownSeconds);
        return ipAllowed;
    }

    private async Task<bool> TryAcquireCoreAsync(string cooldownKey, string quotaKey, int hourlyQuota, int cooldownSeconds)
    {
        var result = await _redis.ScriptEvaluateAsync(AcquireScript, new
        {
            cooldownKey = (RedisKey)cooldownKey,
            quotaKey = (RedisKey)quotaKey,
            hourlyQuota,
            cooldownSeconds
        });

        return (int)result == 1;
    }
}
