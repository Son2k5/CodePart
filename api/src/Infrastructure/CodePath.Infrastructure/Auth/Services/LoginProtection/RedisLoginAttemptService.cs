using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services.LoginProtection;

public sealed class RedisLoginAttemptService : ILoginAttemptService
{
    private readonly IDatabase _redis;
    private readonly InMemoryLoginAttemptService _fallback;
    private readonly LoginSecurityOptions _options;
    private readonly ILogger<RedisLoginAttemptService> _logger;

    private static readonly LuaScript GetDecisionScript = LuaScript.Prepare(@"
        return redis.call('PTTL', @backoffKey)
    ");

    private static readonly LuaScript RecordFailureScript = LuaScript.Prepare(@"
        local pairFailures = redis.call('INCR', @pairFailuresKey)
        if pairFailures == 1 then
            redis.call('EXPIRE', @pairFailuresKey, tonumber(@pairTtlSeconds))
        end

        local backoffSeconds = 0
        local threshold = tonumber(@backoffThreshold)
        if pairFailures >= threshold then
            local exponent = math.min(30, pairFailures - threshold)
            backoffSeconds = math.min(
                tonumber(@maxBackoffSeconds),
                tonumber(@initialBackoffSeconds) * (2 ^ exponent))
            redis.call('SET', @backoffKey, '1', 'EX', backoffSeconds)
        end

        return backoffSeconds
    ");

    public RedisLoginAttemptService(
        IConnectionMultiplexer redis,
        InMemoryLoginAttemptService fallback,
        IOptions<LoginSecurityOptions> options,
        ILogger<RedisLoginAttemptService> logger)
    {
        _redis = redis.GetDatabase();
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public Task<LoginAttemptDecision> GetDecisionAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(
            async () =>
            {
                var result = await _redis.ScriptEvaluateAsync(GetDecisionScript, new
                {
                    backoffKey = (RedisKey)GetBackoffKey(normalizedEmail, ipAddress)
                });
                var ttlMilliseconds = ReadInteger(result);
                return new LoginAttemptDecision(
                    ttlMilliseconds > 0 ? TimeSpan.FromMilliseconds(ttlMilliseconds) : null);
            },
            () => _fallback.GetDecisionAsync(normalizedEmail, ipAddress, cancellationToken));

    public Task<LoginAttemptDecision> RecordFailureAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(
            async () =>
            {
                var result = await _redis.ScriptEvaluateAsync(RecordFailureScript, new
                {
                    pairFailuresKey = (RedisKey)GetPairFailuresKey(normalizedEmail, ipAddress),
                    backoffKey = (RedisKey)GetBackoffKey(normalizedEmail, ipAddress),
                    pairTtlSeconds = checked(_options.FailureCounterTtlMinutes * 60),
                    backoffThreshold = _options.BackoffThreshold,
                    initialBackoffSeconds = _options.InitialBackoffSeconds,
                    maxBackoffSeconds = _options.MaxBackoffSeconds
                });
                var backoffSeconds = checked((int)ReadInteger(result));
                return new LoginAttemptDecision(
                    backoffSeconds > 0 ? TimeSpan.FromSeconds(backoffSeconds) : null);
            },
            () => _fallback.RecordFailureAsync(normalizedEmail, ipAddress, cancellationToken));

    public async Task ResetAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.KeyDeleteAsync(new RedisKey[]
            {
                GetPairFailuresKey(normalizedEmail, ipAddress),
                GetBackoffKey(normalizedEmail, ipAddress)
            });
            await _fallback.ResetAsync(normalizedEmail, ipAddress, cancellationToken);
        }
        catch (Exception exception) when (IsRedisUnavailable(exception))
        {
            LogFallback(exception);
            await _fallback.ResetAsync(normalizedEmail, ipAddress, cancellationToken);
        }
    }

    private async Task<T> ExecuteWithFallbackAsync<T>(
        Func<Task<T>> redisOperation,
        Func<Task<T>> fallbackOperation)
    {
        try
        {
            return await redisOperation();
        }
        catch (Exception exception) when (IsRedisUnavailable(exception))
        {
            LogFallback(exception);
            return await fallbackOperation();
        }
    }

    private void LogFallback(Exception exception) =>
        _logger.LogWarning(
            exception,
            "Redis is unavailable for login protection; using the per-node in-memory fallback.");

    private static bool IsRedisUnavailable(Exception exception) =>
        exception is RedisException or TimeoutException or InvalidOperationException;

    private static long ReadInteger(RedisResult result)
    {
        if (result is null || result.IsNull)
        {
            throw new InvalidOperationException("Redis returned an invalid login-attempt result.");
        }

        return (long)result;
    }

    private static string GetPairFailuresKey(string email, string? ipAddress) =>
        $"auth:login:failures:{NormalizeIp(ipAddress)}:{NormalizeEmail(email)}";

    private static string GetBackoffKey(string email, string? ipAddress) =>
        $"auth:login:backoff:{NormalizeIp(ipAddress)}:{NormalizeEmail(email)}";

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim().ToLowerInvariant();
}
