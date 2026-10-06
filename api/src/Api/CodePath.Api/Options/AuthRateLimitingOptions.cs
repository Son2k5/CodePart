namespace CodePath.Api.Options;

public sealed class AuthRateLimitingOptions
{
    public const string SectionName = "Auth:RateLimiting";

    public int BurstCapacity { get; init; } = 400;
    public int RefillPerMinute { get; init; } = 80;
    public int QueueLimit { get; init; }
}
