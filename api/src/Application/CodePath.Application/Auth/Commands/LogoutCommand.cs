using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Application.Auth.Commands;

public sealed record LogoutCommand(string? RefreshToken, string Jti, DateTime TokenExpiresAtUtc, Guid CurrentUserId) : IRequest<Result<bool>>;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITokenBlacklistService _blacklistService;

    public LogoutCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        ITokenBlacklistService blacklistService)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _blacklistService = blacklistService;
    }

    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
            var token = await _authDbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == request.CurrentUserId, cancellationToken);

            if (token != null && !token.IsRevoked)
            {
                token.Revoke();
                await _authDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var remainingLifetime = request.TokenExpiresAtUtc - DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Jti) && remainingLifetime > TimeSpan.Zero)
        {
            await _blacklistService.BlacklistTokenAsync(request.Jti, remainingLifetime);
        }

        return Result<bool>.Success(true);
    }
}
