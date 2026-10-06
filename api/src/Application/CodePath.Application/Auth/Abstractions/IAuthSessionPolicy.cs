namespace CodePath.Application.Auth.Abstractions;

public interface IAuthSessionPolicy
{
    TimeSpan RefreshTokenLifetime { get; }
    TimeSpan RefreshTokenAbsoluteLifetime { get; }
}
