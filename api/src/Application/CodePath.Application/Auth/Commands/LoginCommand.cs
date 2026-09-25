using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Commands;
using CodePath.Application.Users.Queries;
using CodePath.Domain.Auth.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly ISender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthDbContext _authDbContext;

    private const string GenericAuthErrorMessage = "Email hoặc mật khẩu không chính xác.";

    public LoginCommandHandler(
        ISender sender,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthDbContext authDbContext)
    {
        _sender = sender;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authDbContext = authDbContext;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var userResult = await _sender.Send(new GetUserByEmailQuery(normalizedEmail), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            _passwordHasher.VerifyPassword("dummy", "$2a$12$e8vT9gXhM4kE4D.B1G8q2.j3l5oG7m9qP1rS3tU5vW7xY9zA1bC3e");
            return Result<LoginResponse>.Failure(GenericAuthErrorMessage, "UNAUTHORIZED");
        }

        var user = userResult.Value;

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result<LoginResponse>.Failure(GenericAuthErrorMessage, "UNAUTHORIZED");
        }

        if (!user.EmailVerifiedAt.HasValue)
        {
            return Result<LoginResponse>.Failure("Email chưa được xác thực. Vui lòng xác thực OTP trước khi đăng nhập.", "UNAUTHORIZED");
        }

        switch (user.Status)
        {
            case UserStatus.Pending:
                return Result<LoginResponse>.Failure("Tài khoản Giảng viên đang chờ Quản trị viên cấp quyền.", "ACCOUNT_PENDING");
            case UserStatus.Rejected:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị từ chối.", "ACCOUNT_REJECTED");
            case UserStatus.Disabled:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị vô hiệu hóa.", "ACCOUNT_DISABLED");
            case UserStatus.Active:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _sender.Send(new RecordLoginSuccessCommand(user.Id), cancellationToken);

        var tokens = _jwtTokenService.GenerateTokens(user.Id, user.Email, user.Role, user.Status);
        var tokenHash = _jwtTokenService.HashRefreshToken(tokens.RefreshToken);

        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            tokenHash,
            DateTime.UtcNow.AddDays(7));

        await _authDbContext.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds));
    }
}
