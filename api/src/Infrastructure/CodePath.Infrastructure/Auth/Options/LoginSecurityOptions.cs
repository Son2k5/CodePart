namespace CodePath.Infrastructure.Auth.Options;

public sealed class LoginSecurityOptions
{
    public const string SectionName = "Auth:LoginProtection";

    public int BackoffThreshold { get; init; } = 5;
    public int FailureCounterTtlMinutes { get; init; } = 15;
    public int InitialBackoffSeconds { get; init; } = 2;
    public int MaxBackoffSeconds { get; init; } = 60;
}
