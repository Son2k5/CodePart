using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record LogoutCommand(
    string? RefreshToken,
    string? Jti = null,
    DateTime? TokenExpiresAtUtc = null,
    string? IpAddress = null) : IRequest<Result<bool>>;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly TimeProvider _timeProvider;

    public LogoutCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        ITokenBlacklistService blacklistService,
        TimeProvider timeProvider)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _blacklistService = blacklistService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
            await _authDbContext.RevokeRefreshTokenAsync(
                hash,
                utcNow,
                request.IpAddress,
                cancellationToken);
        }

        var remainingLifetime = request.TokenExpiresAtUtc.GetValueOrDefault() - utcNow;
        if (!string.IsNullOrWhiteSpace(request.Jti) && remainingLifetime > TimeSpan.Zero)
        {
            await _blacklistService.BlacklistTokenAsync(request.Jti, remainingLifetime);
        }

        return Result<bool>.Success(true);
    }
}
