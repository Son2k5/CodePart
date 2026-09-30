namespace CodePath.Infrastructure.Auth.Options;

public sealed class LoginSecurityOptions
{
    public const string SectionName = "AuthSecurity:Login";

    public int MaxFailures { get; init; } = 5;
    public int FailureWindowMinutes { get; init; } = 15;
    public int LockoutMinutes { get; init; } = 15;
}
