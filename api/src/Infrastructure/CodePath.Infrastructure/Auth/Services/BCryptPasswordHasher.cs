using CodePath.Application.Auth.Abstractions;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString(), workFactor: 12);

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);

    public void SimulateVerification() =>
        BCrypt.Net.BCrypt.EnhancedVerify("dummy", DummyHash);
}
