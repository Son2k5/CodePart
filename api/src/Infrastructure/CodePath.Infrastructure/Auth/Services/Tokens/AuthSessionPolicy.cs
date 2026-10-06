using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.Tokens;

public sealed class AuthSessionPolicy : IAuthSessionPolicy
{
    public AuthSessionPolicy(IOptions<AuthSessionOptions> options)
    {
        RefreshTokenLifetime = TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays);
        RefreshTokenAbsoluteLifetime = TimeSpan.FromDays(options.Value.RefreshTokenAbsoluteLifetimeDays);
    }

    public TimeSpan RefreshTokenLifetime { get; }
    public TimeSpan RefreshTokenAbsoluteLifetime { get; }
}
