using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Auth.Abstractions;

public record GeneratedTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public interface IJwtTokenService
{
    GeneratedTokens GenerateTokens(Guid userId, string email, UserRole role, UserStatus status);
    string HashRefreshToken(string refreshToken);
}
