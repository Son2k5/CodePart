using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record LogoutAllCommand(
    Guid UserId,
    string? Jti = null,
    DateTime? TokenExpiresAtUtc = null,
    string? IpAddress = null) : IRequest<Result<bool>>;

internal sealed class LogoutAllCommandHandler : IRequestHandler<LogoutAllCommand, Result<bool>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly TimeProvider _timeProvider;

    public LogoutAllCommandHandler(
        IAuthDbContext authDbContext,
        ITokenBlacklistService blacklistService,
        TimeProvider timeProvider)
    {
        _authDbContext = authDbContext;
        _blacklistService = blacklistService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<bool>> Handle(LogoutAllCommand request, CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        await _authDbContext.RevokeAllUserRefreshTokensAsync(
            request.UserId,
            utcNow,
            request.IpAddress,
            cancellationToken);

        var remainingLifetime = request.TokenExpiresAtUtc.GetValueOrDefault() - utcNow;
        if (!string.IsNullOrWhiteSpace(request.Jti) && remainingLifetime > TimeSpan.Zero)
        {
            await _blacklistService.BlacklistTokenAsync(request.Jti, remainingLifetime);
        }

        return Result<bool>.Success(true);
    }
}
