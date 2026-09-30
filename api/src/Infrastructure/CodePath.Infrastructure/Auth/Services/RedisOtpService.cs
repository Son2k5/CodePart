using System.Security.Cryptography;
using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class RedisOtpService : IOtpService
{
    private const int OtpTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int CooldownSeconds = 60;
    private const int HourlyQuota = 5;
    private readonly IDatabase _redis;

    private static readonly LuaScript IssueOtpScript = LuaScript.Prepare(@"
        local otpKey = @otpKey
        local attemptsKey = @attemptsKey
        local cooldownKey = @cooldownKey
        local quotaKey = @quotaKey
        local newOtp = @newOtp
        local otpTtlSeconds = tonumber(@otpTtlSeconds)
        local maxAttempts = tonumber(@maxAttempts)
        local cooldownSeconds = tonumber(@cooldownSeconds)
        local hourlyQuota = tonumber(@hourlyQuota)

        if redis.call('EXISTS', otpKey) == 1 or redis.call('EXISTS', cooldownKey) == 1 then
            return { 0, '' }
        end

        local quota = redis.call('INCR', quotaKey)
        if quota == 1 then
            redis.call('EXPIRE', quotaKey, 3600)
        end

        if quota > hourlyQuota then
            return { 0, '' }
        end

        redis.call('SET', otpKey, newOtp, 'EX', otpTtlSeconds)
        redis.call('SET', attemptsKey, maxAttempts, 'EX', otpTtlSeconds)
        redis.call('SET', cooldownKey, '1', 'EX', cooldownSeconds)
        return { 1, newOtp }
    ");

    private static readonly LuaScript InvalidateOtpScript = LuaScript.Prepare(@"
        if redis.call('GET', @otpKey) == @expectedOtp then
            redis.call('DEL', @otpKey, @attemptsKey, @cooldownKey)
            return 1
        end
        return 0
    ");

    private static readonly LuaScript VerifyOtpScript = LuaScript.Prepare(@"
        local otpKey = @otpKey
        local attemptsKey = @attemptsKey
        local cooldownKey = @cooldownKey
        local inputOtp = @inputOtp

        local storedOtp = redis.call('GET', otpKey)
        if not storedOtp then
            return { -1, 'OTP has expired or does not exist.' }
        end

        local attempts = tonumber(redis.call('GET', attemptsKey) or '0')
        if attempts <= 0 then
            redis.call('DEL', otpKey, attemptsKey)
            return { -2, 'Too many invalid OTP attempts. Request a new code.' }
        end

        if storedOtp ~= inputOtp then
            attempts = redis.call('DECR', attemptsKey)
            if attempts <= 0 then
                redis.call('DEL', otpKey, attemptsKey)
                return { -2, 'Too many invalid OTP attempts. Request a new code.' }
            end
            return { 0, tostring(attempts) }
        end

        redis.call('DEL', otpKey, attemptsKey, cooldownKey)
        return { 1, 'SUCCESS' }
    ");

    public RedisOtpService(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task<(bool Issued, string? Otp)> TryIssueOtpAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(IssueOtpScript, new
        {
            otpKey = (RedisKey)GetOtpKey(normalizedEmail),
            attemptsKey = (RedisKey)GetAttemptsKey(normalizedEmail),
            cooldownKey = (RedisKey)GetCooldownKey(normalizedEmail),
            quotaKey = (RedisKey)GetQuotaKey(normalizedEmail),
            newOtp = otp,
            otpTtlSeconds = OtpTtlMinutes * 60,
            maxAttempts = MaxAttempts,
            cooldownSeconds = CooldownSeconds,
            hourlyQuota = HourlyQuota
        });

        if (result is null || result.Length < 2)
        {
            throw new InvalidOperationException("Redis returned an invalid OTP issue result.");
        }

        return ((int)result[0] == 1, (string?)result[1]);
    }

    public async Task InvalidateOtpAsync(string email, string otp)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        await _redis.ScriptEvaluateAsync(InvalidateOtpScript, new
        {
            otpKey = (RedisKey)GetOtpKey(normalizedEmail),
            attemptsKey = (RedisKey)GetAttemptsKey(normalizedEmail),
            cooldownKey = (RedisKey)GetCooldownKey(normalizedEmail),
            expectedOtp = otp
        });
    }

    public async Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(VerifyOtpScript, new
        {
            otpKey = (RedisKey)GetOtpKey(normalizedEmail),
            attemptsKey = (RedisKey)GetAttemptsKey(normalizedEmail),
            cooldownKey = (RedisKey)GetCooldownKey(normalizedEmail),
            inputOtp = otp.Trim()
        });

        if (result is null || result.Length < 2)
        {
            return (false, "The OTP service returned an invalid response.");
        }

        var status = (int)result[0];
        var message = (string?)result[1];
        return status switch
        {
            1 => (true, null),
            0 => (false, $"The OTP is incorrect. {message} attempts remain."),
            _ => (false, message ?? "The OTP is invalid.")
        };
    }

    private static string GetOtpKey(string email) => $"auth:otp:{email}";
    private static string GetAttemptsKey(string email) => $"auth:otp:attempts:{email}";
    private static string GetCooldownKey(string email) => $"auth:otp:cooldown:{email}";
    private static string GetQuotaKey(string email) => $"auth:otp:quota:{email}";
}
