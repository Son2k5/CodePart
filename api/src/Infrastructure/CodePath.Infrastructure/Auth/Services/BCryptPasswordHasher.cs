using CodePath.Application.Auth.Abstractions;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
}
