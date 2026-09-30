using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class RedisLoginAttemptService : ILoginAttemptService
{
    private readonly IDatabase _redis;
    private readonly LoginSecurityOptions _options;

    private static readonly LuaScript RecordFailureScript = LuaScript.Prepare(@"
        local failuresKey = @failuresKey
        local lockKey = @lockKey
        local maxFailures = tonumber(@maxFailures)
        local failureWindowSeconds = tonumber(@failureWindowSeconds)
        local lockoutSeconds = tonumber(@lockoutSeconds)

        local existingLockTtl = redis.call('TTL', lockKey)
        if existingLockTtl > 0 then
            return { 1, existingLockTtl }
        end

        local failures = redis.call('INCR', failuresKey)
        if failures == 1 then
            redis.call('EXPIRE', failuresKey, failureWindowSeconds)
        end

        if failures >= maxFailures then
            redis.call('SET', lockKey, '1', 'EX', lockoutSeconds)
            redis.call('DEL', failuresKey)
            return { 1, lockoutSeconds }
        end

        return { 0, 0 }
    ");

    public RedisLoginAttemptService(
        IConnectionMultiplexer redis,
        IOptions<LoginSecurityOptions> options)
    {
        _redis = redis.GetDatabase();
        _options = options.Value;
    }

    public async Task<TimeSpan?> GetLockoutRemainingAsync(string normalizedEmail)
        => await _redis.KeyTimeToLiveAsync(GetLockKey(normalizedEmail));

    public async Task<LoginFailureResult> RecordFailureAsync(string normalizedEmail)
    {
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(RecordFailureScript, new
        {
            failuresKey = (RedisKey)GetFailuresKey(normalizedEmail),
            lockKey = (RedisKey)GetLockKey(normalizedEmail),
            maxFailures = _options.MaxFailures,
            failureWindowSeconds = checked(_options.FailureWindowMinutes * 60),
            lockoutSeconds = checked(_options.LockoutMinutes * 60)
        });

        if (result is null || result.Length < 2)
        {
            throw new InvalidOperationException("Redis returned an invalid login-attempt result.");
        }

        var isLocked = (int)result[0] == 1;
        var retryAfterSeconds = (int)result[1];
        return new LoginFailureResult(
            isLocked,
            isLocked ? TimeSpan.FromSeconds(Math.Max(1, retryAfterSeconds)) : null);
    }

    public Task ResetAsync(string normalizedEmail)
        => _redis.KeyDeleteAsync(new RedisKey[]
        {
            GetFailuresKey(normalizedEmail),
            GetLockKey(normalizedEmail)
        });

    private static string GetFailuresKey(string email) => $"auth:login:failures:{email.ToLowerInvariant()}";
    private static string GetLockKey(string email) => $"auth:login:lock:{email.ToLowerInvariant()}";
}
