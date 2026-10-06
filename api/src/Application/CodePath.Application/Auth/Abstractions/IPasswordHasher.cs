namespace CodePath.Application.Auth.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    bool NeedsRehash(string passwordHash);
    void SimulateVerification();
}
