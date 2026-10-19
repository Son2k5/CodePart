using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.Tokens;

public sealed class AuthSessionPolicy : IAuthSessionPolicy
{
    public AuthSessionPolicy(IOptions<AuthSessionOptions> options)
    {
        IdleTimeout = TimeSpan.FromMinutes(options.Value.IdleTimeoutMinutes);
        ActivityUpdateThrottle = TimeSpan.FromMinutes(options.Value.ActivityUpdateThrottleMinutes);
        RefreshTokenLifetime = TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays);
        RefreshTokenAbsoluteLifetime = TimeSpan.FromDays(options.Value.RefreshTokenAbsoluteLifetimeDays);
    }

    public TimeSpan IdleTimeout { get; }
    public TimeSpan ActivityUpdateThrottle { get; }
    public TimeSpan RefreshTokenLifetime { get; }
    public TimeSpan RefreshTokenAbsoluteLifetime { get; }
}
