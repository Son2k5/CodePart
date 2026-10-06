using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<Result<LoginResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUsersDbContext _usersDbContext;
    private readonly IAuthSessionPolicy _authSessionPolicy;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        IUsersDbContext usersDbContext,
        IAuthSessionPolicy authSessionPolicy,
        TimeProvider timeProvider)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _usersDbContext = usersDbContext;
        _authSessionPolicy = authSessionPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var existingToken = await _authDbContext.GetRefreshTokenAsync(tokenHash, cancellationToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        if (existingToken is null)
        {
            return Result<LoginResponse>.Failure("Refresh token is invalid.", ErrorCodes.Unauthorized);
        }

        if (existingToken.IsRevoked)
        {
            await RevokeFamilyAsync(existingToken, request.IpAddress, cancellationToken);
            return Result<LoginResponse>.Failure(
                "Refresh token reuse detected. The token family has been revoked.",
                ErrorCodes.Unauthorized);
        }

        if (existingToken.IsExpiredAt(utcNow) || utcNow >= existingToken.AbsoluteExpiresAt)
        {
            return Result<LoginResponse>.Failure("Refresh token has expired. Please sign in again.", ErrorCodes.Unauthorized);
        }

        var user = await _usersDbContext.GetByIdReadOnlyAsync(existingToken.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            await RevokeFamilyAsync(existingToken, request.IpAddress, cancellationToken);
            return Result<LoginResponse>.Failure("The user is invalid or is not active.", ErrorCodes.Forbidden);
        }

        var newTokens = _jwtTokenService.GenerateTokens(
            user.Id, user.Email, user.Role, user.Status, user.SecurityVersion);
        var newTokenHash = _jwtTokenService.HashRefreshToken(newTokens.RefreshToken);
        var replacement = RefreshToken.CreateReplacement(
            existingToken,
            newTokenHash,
            utcNow,
            _authSessionPolicy.RefreshTokenLifetime,
            request.IpAddress);

        var rotated = await _authDbContext.TryRotateRefreshTokenAsync(
            tokenHash,
            replacement,
            utcNow,
            request.IpAddress,
            cancellationToken);

        if (!rotated)
        {
            await RevokeFamilyAsync(existingToken, request.IpAddress, cancellationToken);
            return Result<LoginResponse>.Failure(
                "Refresh token is invalid or has already been used. The token family has been revoked.",
                ErrorCodes.Unauthorized);
        }

        return Result<LoginResponse>.Success(new LoginResponse(
            newTokens.AccessToken,
            newTokens.ExpiresInSeconds,
            AuthenticatedUserResponse.From(user))
        {
            RefreshToken = newTokens.RefreshToken,
            RefreshTokenExpiresAt = replacement.ExpiresAt
        });
    }

    private Task<int> RevokeFamilyAsync(
        RefreshToken token,
        string? ipAddress,
        CancellationToken cancellationToken)
        => _authDbContext.RevokeTokenFamilyAsync(
            token.FamilyId,
            _timeProvider.GetUtcNow().UtcDateTime,
            ipAddress,
            cancellationToken);

}
