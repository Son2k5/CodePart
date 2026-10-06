namespace CodePath.Infrastructure.Auth.Options;

public sealed class LoginConcurrencyOptions
{
    public const string SectionName = "Auth:LoginConcurrency";

    public int MaxConcurrentLogins { get; init; } = Math.Max(1, Environment.ProcessorCount);
    public int QueueLimit { get; init; } = 300;
    public int QueueTimeoutSeconds { get; init; } = 10;
    public int MaxConcurrentPerIp { get; init; } = 4;
}
