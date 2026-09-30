using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;
using System.Text.Json.Serialization;

namespace CodePath.Application.Auth.Commands;

public sealed record LoginResponse(string AccessToken, int ExpiresIn, string TokenType = "Bearer")
{
    [JsonIgnore]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonIgnore]
    public DateTime RefreshTokenExpiresAt { get; init; }
}

public sealed record LoginCommand(string Email, string Password, string? IpAddress = null) : IRequest<Result<LoginResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUsersDbContext _usersDbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthDbContext _authDbContext;
    private readonly ILoginAttemptService _loginAttemptService;
    private readonly TimeProvider _timeProvider;

    private const string GenericAuthErrorMessage = "Email hoặc mật khẩu không chính xác.";

    public LoginCommandHandler(
        IUsersDbContext usersDbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthDbContext authDbContext,
        ILoginAttemptService loginAttemptService,
        TimeProvider timeProvider)
    {
        _usersDbContext = usersDbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authDbContext = authDbContext;
        _loginAttemptService = loginAttemptService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        var lockoutRemaining = await _loginAttemptService.GetLockoutRemainingAsync(normalizedEmail);
        if (lockoutRemaining.HasValue && lockoutRemaining.Value > TimeSpan.Zero)
        {
            return CreateLockoutResult(lockoutRemaining.Value);
        }

        var user = await _usersDbContext.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            _passwordHasher.SimulateVerification();
            return await RecordFailureAsync(normalizedEmail);
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return await RecordFailureAsync(normalizedEmail);
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

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        user.RecordLoginSuccess(utcNow);

        var tokens = _jwtTokenService.GenerateTokens(user.Id, user.Email, user.Role, user.Status);
        var tokenHash = _jwtTokenService.HashRefreshToken(tokens.RefreshToken);

        var refreshTokenEntity = RefreshToken.CreateWithDefaultLifetime(
            user.Id,
            tokenHash,
            utcNow,
            request.IpAddress);

        await _authDbContext.AddRefreshTokenAsync(refreshTokenEntity, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);
        await _loginAttemptService.ResetAsync(normalizedEmail);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.ExpiresInSeconds)
        {
            RefreshToken = tokens.RefreshToken,
            RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt
        });
    }

    private async Task<Result<LoginResponse>> RecordFailureAsync(string normalizedEmail)
    {
        var failure = await _loginAttemptService.RecordFailureAsync(normalizedEmail);
        return failure.IsLocked
            ? CreateLockoutResult(failure.RetryAfter ?? TimeSpan.FromMinutes(15))
            : Result<LoginResponse>.Failure(GenericAuthErrorMessage, ErrorCodes.Unauthorized);
    }

    private static Result<LoginResponse> CreateLockoutResult(TimeSpan remaining)
        => Result<LoginResponse>.Failure(
            $"Too many failed login attempts. Try again in {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} minute(s).",
            ErrorCodes.TooManyRequests);
}
