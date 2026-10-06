namespace CodePath.Application.Auth;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password)
            || password.Length < MinimumLength
            || password.Length > MaximumLength)
        {
            return false;
        }

        return password.Any(char.IsLower)
            && password.Any(char.IsUpper)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsLetterOrDigit(character));
    }
}
