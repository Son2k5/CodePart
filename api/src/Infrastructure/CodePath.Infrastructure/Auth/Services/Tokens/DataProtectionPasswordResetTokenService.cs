using System.Security.Cryptography;
using System.Text;
using CodePath.Application.Auth.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace CodePath.Infrastructure.Auth.Services.Tokens;

/// <summary>
/// Stateless reset tokens via <see cref="IDataProtector"/> (purpose "PasswordReset",
/// 15-minute TTL encoded in the payload). Payload is <c>userId|fingerprint|expiresAt</c>;
/// the fingerprint binds the token to the password hash that existed at issue time, so any
/// reset (or admin password change) invalidates outstanding links without a token table.
/// </summary>
public sealed class DataProtectionPasswordResetTokenService : IPasswordResetTokenService
{
    public const string Purpose = "PasswordReset";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;

    public DataProtectionPasswordResetTokenService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Create(Guid userId, string currentPasswordHash)
    {
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime).ToString("O");
        var payload = $"{userId:N}|{PasswordFingerprint.Compute(currentPasswordHash)}|{expiresAt}";
        return _protector.Protect(payload);
    }

    public bool TryValidate(string token, out Guid userId, out string fingerprint)
    {
        userId = Guid.Empty;
        fingerprint = string.Empty;
        string payload;
        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (Exception)
        {
            // Tampered or wrong-key-ring tokens fail validation identically.
            return false;
        }

        var parts = payload.Split('|', 3);
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out userId) || parts[1].Length != 16)
        {
            userId = Guid.Empty;
            return false;
        }

        // Check expiry embedded in the protected payload
        if (!DateTime.TryParse(parts[2], null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt)
            || DateTime.UtcNow > expiresAt)
        {
            userId = Guid.Empty;
            return false;
        }

        fingerprint = parts[1];
        return true;
    }
}

/// <summary>SHA-256 fingerprint (first 16 hex chars) of the current password hash.</summary>
public static class PasswordFingerprint
{
    public static string Compute(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];

    public static bool Matches(string currentHash, string expectedFingerprint)
    {
        var current = Encoding.UTF8.GetBytes(Compute(currentHash));
        var expected = Encoding.UTF8.GetBytes(expectedFingerprint);
        return current.Length == expected.Length && CryptographicOperations.FixedTimeEquals(current, expected);
    }
}
