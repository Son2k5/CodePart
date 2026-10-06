namespace CodePath.Infrastructure.Auth.Options;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "Auth:PasswordHashing";

    public int WorkFactor { get; init; } = 12;
}
