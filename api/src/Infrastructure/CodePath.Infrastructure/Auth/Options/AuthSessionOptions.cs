namespace CodePath.Infrastructure.Auth.Options;

public sealed class AuthSessionOptions
{
    public const string SectionName = "Auth:Session";

    public int IdleTimeoutMinutes { get; init; } = 60;
    public int ActivityUpdateThrottleMinutes { get; init; } = 5;
    public int RefreshTokenLifetimeDays { get; init; } = 7;
    public int RefreshTokenAbsoluteLifetimeDays { get; init; } = 7;
    public string CookieSameSite { get; init; } = "Strict";
    public int CleanupIntervalHours { get; init; } = 24;
    public int ExpiredTokenRetentionDays { get; init; } = 30;
}
