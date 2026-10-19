namespace CodePath.Application.Auth.Abstractions;

public interface IAuthSessionPolicy
{
    TimeSpan IdleTimeout { get; }
    TimeSpan ActivityUpdateThrottle { get; }
    TimeSpan RefreshTokenLifetime { get; }
    TimeSpan RefreshTokenAbsoluteLifetime { get; }
}
