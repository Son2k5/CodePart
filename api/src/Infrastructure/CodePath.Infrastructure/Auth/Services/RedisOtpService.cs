using System.Security.Cryptography;
using CodePath.Application.Auth.Abstractions;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class RedisOtpService : IOtpService
{
    private readonly IDatabase _redis;
    private const int OtpTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int CooldownSeconds = 60;

    private static readonly LuaScript VerifyOtpScript = LuaScript.Prepare(@"
        local otpKey = @otpKey
        local attemptsKey = @attemptsKey
        local cooldownKey = @cooldownKey
        local inputOtp = @inputOtp

        local storedOtp = redis.call('GET', otpKey)
        if not storedOtp then
            return { -1, 'OTP đã hết hạn hoặc không tồn tại.' }
        end

        local attempts = tonumber(redis.call('GET', attemptsKey) or '0')
        if attempts <= 0 then
            redis.call('DEL', otpKey, attemptsKey)
            return { -2, 'Bạn đã nhập sai OTP quá số lần quy định. Vui lòng yêu cầu lại mã.' }
        end

        if storedOtp ~= inputOtp then
            attempts = redis.call('DECR', attemptsKey)
            if attempts <= 0 then
                redis.call('DEL', otpKey, attemptsKey)
                return { -2, 'Bạn đã nhập sai OTP quá số lần quy định. Vui lòng yêu cầu lại mã.' }
            end
            return { 0, tostring(attempts) }
        end

        redis.call('DEL', otpKey, attemptsKey, cooldownKey)
        return { 1, 'SUCCESS' }
    ");

    public RedisOtpService(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task<(bool Success, string? ErrorMessage)> CanRequestOtpAsync(string email)
    {
        var cooldownKey = $"auth:otp:cooldown:{email.ToLowerInvariant()}";
        if (await _redis.KeyExistsAsync(cooldownKey))
        {
            var ttl = await _redis.KeyTimeToLiveAsync(cooldownKey);
            return (false, $"Vui lòng đợi {ttl?.Seconds ?? 60}s trước khi yêu cầu lại OTP.");
        }
        return (true, null);
    }

    public async Task<string> GenerateAndStoreOtpAsync(string email)
    {
        var normEmail = email.ToLowerInvariant();
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var otpKey = $"auth:otp:{normEmail}";
        var attemptsKey = $"auth:otp:attempts:{normEmail}";
        var cooldownKey = $"auth:otp:cooldown:{normEmail}";

        var tran = _redis.CreateTransaction();
        _ = tran.StringSetAsync(otpKey, otp, TimeSpan.FromMinutes(OtpTtlMinutes));
        _ = tran.StringSetAsync(attemptsKey, MaxAttempts, TimeSpan.FromMinutes(OtpTtlMinutes));
        _ = tran.StringSetAsync(cooldownKey, "1", TimeSpan.FromSeconds(CooldownSeconds));
        
        var committed = await tran.ExecuteAsync();
        if (!committed)
        {
            throw new InvalidOperationException("Không thể khởi tạo mã OTP trong Redis.");
        }

        return otp;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> VerifyOtpAsync(string email, string otp)
    {
        var normEmail = email.ToLowerInvariant();
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(VerifyOtpScript, new
        {
            otpKey = (RedisKey)$"auth:otp:{normEmail}",
            attemptsKey = (RedisKey)$"auth:otp:attempts:{normEmail}",
            cooldownKey = (RedisKey)$"auth:otp:cooldown:{normEmail}",
            inputOtp = otp.Trim()
        });

        if (result == null || result.Length < 2)
            return (false, "Lỗi kiểm tra OTP từ máy chủ.");

        var status = (int)result[0];
        var message = (string?)result[1];

        return status switch
        {
            1 => (true, null),
            0 => (false, $"Mã OTP không chính xác. Bạn còn {message} lần thử."),
            _ => (false, message ?? "Mã OTP không hợp lệ.")
        };
    }
}
