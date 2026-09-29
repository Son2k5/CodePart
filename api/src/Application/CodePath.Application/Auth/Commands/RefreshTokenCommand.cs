using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Queries;
using CodePath.Domain.Auth.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IAuthDbContext _authDbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISender _sender;

    public RefreshTokenCommandHandler(
        IAuthDbContext authDbContext,
        IJwtTokenService jwtTokenService,
        ISender sender)
    {
        _authDbContext = authDbContext;
        _jwtTokenService = jwtTokenService;
        _sender = sender;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);

        var existingToken = await _authDbContext.GetRefreshTokenAsync(tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result<LoginResponse>.Failure("Refresh token không hợp lệ.", ErrorCodes.Unauthorized);
        }

        if (existingToken.IsRevoked)
        {
            if (existingToken.IsWithinReuseGracePeriod())
            {
                var activeReplacement = await _authDbContext.GetActiveRefreshTokenAsync(
                    existingToken.ReplacedByTokenHash!, cancellationToken);

                if (activeReplacement != null && !activeReplacement.IsExpired)
                {
                    var userRes = await _sender.Send(new GetUserByIdQuery(existingToken.UserId), cancellationToken);
                    if (userRes.IsSuccess && userRes.Value != null && userRes.Value.Status == UserStatus.Active)
                    {
                        var u = userRes.Value;
                        var freshTokens = _jwtTokenService.GenerateTokens(u.Id, u.Email, u.Role, u.Status);
                        var freshTokenHash = _jwtTokenService.HashRefreshToken(freshTokens.RefreshToken);

                        var branchToken = RefreshToken.CreateWithDefaultLifetime(
                            u.Id,
                            freshTokenHash);

                        await _authDbContext.AddRefreshTokenAsync(branchToken, cancellationToken);
                        await _authDbContext.SaveChangesAsync(cancellationToken);

                        return Result<LoginResponse>.Success(new LoginResponse(
                            freshTokens.AccessToken,
                            freshTokens.RefreshToken,
                            freshTokens.ExpiresInSeconds));
                    }
                }
            }

            var userTokens = await _authDbContext.GetActiveUserTokensAsync(existingToken.UserId, cancellationToken);

            foreach (var t in userTokens)
            {
                t.Revoke();
            }

            await _authDbContext.SaveChangesAsync(cancellationToken);
            return Result<LoginResponse>.Failure("Cảnh báo bảo mật: Token đã thu hồi bị sử dụng lại ngoài thời gian cho phép. Mọi phiên làm việc đã bị hủy.", ErrorCodes.Unauthorized);
        }

        if (existingToken.IsExpired)
        {
            return Result<LoginResponse>.Failure("Refresh token đã hết hạn. Vui lòng đăng nhập lại.", ErrorCodes.Unauthorized);
        }

        var userResult = await _sender.Send(new GetUserByIdQuery(existingToken.UserId), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null || userResult.Value.Status != UserStatus.Active)
        {
            return Result<LoginResponse>.Failure("Người dùng không hợp lệ hoặc không ở trạng thái Active.", ErrorCodes.Forbidden);
        }

        var user = userResult.Value;

        var newTokens = _jwtTokenService.GenerateTokens(user.Id, user.Email, user.Role, user.Status);
        var newTokenHash = _jwtTokenService.HashRefreshToken(newTokens.RefreshToken);

        existingToken.Revoke(replacedByTokenHash: newTokenHash);

        var newRefreshToken = RefreshToken.CreateWithDefaultLifetime(
            user.Id,
            newTokenHash);

        await _authDbContext.AddRefreshTokenAsync(newRefreshToken, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            newTokens.AccessToken,
            newTokens.RefreshToken,
            newTokens.ExpiresInSeconds));
    }
}
