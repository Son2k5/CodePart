using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public GeneratedTokens GenerateTokens(Guid userId, string email, UserRole role, UserStatus status)
    {
        var secret = _configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        var issuer = _configuration["Jwt:Issuer"] ?? "CodePath.Api";
        var audience = _configuration["Jwt:Audience"] ?? "CodePath.Client";
        var expiryMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpiryMinutes"], out var mins) ? mins : 15;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secret);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role.ToString()),
            new("status", status.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var refreshToken = Convert.ToBase64String(randomBytes);

        return new GeneratedTokens(accessToken, refreshToken, expiryMinutes * 60);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
