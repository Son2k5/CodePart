using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.Passwords;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;
    private readonly string _dummyHash;

    public BCryptPasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        _workFactor = options.Value.WorkFactor;
        _dummyHash = BCrypt.Net.BCrypt.EnhancedHashPassword(
            "CodePath constant dummy password",
            workFactor: _workFactor);
    }

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: _workFactor);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);

    public bool NeedsRehash(string passwordHash) =>
        BCrypt.Net.BCrypt.PasswordNeedsRehash(passwordHash, _workFactor);

    public void SimulateVerification() =>
        BCrypt.Net.BCrypt.EnhancedVerify("invalid password", _dummyHash);
}
