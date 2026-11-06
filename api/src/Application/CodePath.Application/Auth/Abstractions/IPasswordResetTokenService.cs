namespace CodePath.Application.Auth.Abstractions;

/// <summary>
/// Stateless password reset tokens: self-describing, time-limited, tamper-proof payloads
/// (ASP.NET Core Data Protection behind the scenes — no database token table required).
/// </summary>
public interface IPasswordResetTokenService
{
    /// <summary>Creates a reset token bound to the user and a fingerprint of their current password hash.</summary>
    string Create(Guid userId, string currentPasswordHash);

    /// <summary>Validates signature and lifetime; extracts the user id and password fingerprint from the payload.</summary>
    bool TryValidate(string token, out Guid userId, out string fingerprint);
}
