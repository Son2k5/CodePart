using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;
using System.Text.Json.Serialization;
using CodePath.Application.Auth;

namespace CodePath.Application.Auth.Commands;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string Status,
    DateTime? EmailVerifiedAt)
{
    internal static AuthenticatedUserResponse From(User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role.ToString(),
        user.Status.ToString(),
        user.EmailVerifiedAt);
}

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    AuthenticatedUserResponse User,
    string TokenType = "Bearer")
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
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordPolicy.MaximumLength);
    }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUsersDbContext _usersDbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthDbContext _authDbContext;
    private readonly ILoginAttemptService _loginAttemptService;
    private readonly IAuthSessionPolicy _authSessionPolicy;
    private readonly ILoginConcurrencyLimiter _loginConcurrencyLimiter;
    private readonly TimeProvider _timeProvider;

    private const string GenericAuthErrorMessage = "Email hoặc mật khẩu không chính xác.";

    public LoginCommandHandler(
        IUsersDbContext usersDbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthDbContext authDbContext,
        ILoginAttemptService loginAttemptService,
        IAuthSessionPolicy authSessionPolicy,
        ILoginConcurrencyLimiter loginConcurrencyLimiter,
        TimeProvider timeProvider)
    {
        _usersDbContext = usersDbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authDbContext = authDbContext;
        _loginAttemptService = loginAttemptService;
        _authSessionPolicy = authSessionPolicy;
        _loginConcurrencyLimiter = loginConcurrencyLimiter;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        var attemptDecision = await _loginAttemptService.GetDecisionAsync(
            normalizedEmail,
            request.IpAddress,
            cancellationToken);
        if (attemptDecision.IsInBackoff)
        {
            return CreateBackoffResult(attemptDecision);
        }

        var user = await _usersDbContext.GetByEmailAsync(normalizedEmail, cancellationToken);
        var concurrency = await _loginConcurrencyLimiter.AcquireAsync(
            request.IpAddress,
            cancellationToken);
        if (!concurrency.IsAcquired)
        {
            return Result<LoginResponse>.Failure(
                "The login service is busy. Please try again shortly.",
                ErrorCodes.TooManyRequests,
                concurrency.RetryAfter);
        }

        bool isPasswordValid;
        string? upgradedPasswordHash = null;
        await using (concurrency.Lease!)
        {
            if (user is null)
            {
                _passwordHasher.SimulateVerification();
                isPasswordValid = false;
            }
            else
            {
                isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
                if (isPasswordValid && _passwordHasher.NeedsRehash(user.PasswordHash))
                {
                    upgradedPasswordHash = _passwordHasher.HashPassword(request.Password);
                }
            }
        }

        if (user is null || !isPasswordValid)
        {
            return await RecordFailureAsync(normalizedEmail, request.IpAddress, cancellationToken);
        }

        if (!user.EmailVerifiedAt.HasValue)
        {
            return Result<LoginResponse>.Failure(
                "Email chưa được xác thực. Vui lòng xác thực OTP trước khi đăng nhập.",
                ErrorCodes.AccountUnverified);
        }

        switch (user.Status)
        {
            case UserStatus.Pending:
                return Result<LoginResponse>.Failure("Tài khoản Giảng viên đang chờ Quản trị viên cấp quyền.", ErrorCodes.AccountPending);
            case UserStatus.Rejected:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị từ chối.", ErrorCodes.AccountRejected);
            case UserStatus.Disabled:
                return Result<LoginResponse>.Failure("Tài khoản của bạn đã bị vô hiệu hóa.", ErrorCodes.AccountDisabled);
            case UserStatus.Active:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        user.RecordLoginSuccess(utcNow);

        if (upgradedPasswordHash is not null)
        {
            user.UpgradePasswordHash(upgradedPasswordHash, utcNow);
        }

        var tokens = _jwtTokenService.GenerateTokens(
            user.Id, user.Email, user.Role, user.Status, user.SecurityVersion);
        var tokenHash = _jwtTokenService.HashRefreshToken(tokens.RefreshToken);

        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            tokenHash,
            utcNow.Add(_authSessionPolicy.RefreshTokenLifetime),
            utcNow.Add(_authSessionPolicy.RefreshTokenAbsoluteLifetime),
            utcNow,
            createdByIp: request.IpAddress);

        await _authDbContext.AddRefreshTokenAsync(refreshTokenEntity, cancellationToken);
        await _authDbContext.SaveChangesAsync(cancellationToken);
        await _loginAttemptService.ResetAsync(normalizedEmail, request.IpAddress, cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            tokens.AccessToken,
            tokens.ExpiresInSeconds,
            AuthenticatedUserResponse.From(user))
        {
            RefreshToken = tokens.RefreshToken,
            RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt
        });
    }

    private async Task<Result<LoginResponse>> RecordFailureAsync(
        string normalizedEmail,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var decision = await _loginAttemptService.RecordFailureAsync(
            normalizedEmail,
            ipAddress,
            cancellationToken);
        return decision.IsInBackoff
            ? CreateBackoffResult(decision)
            : Result<LoginResponse>.Failure(
                GenericAuthErrorMessage,
                ErrorCodes.Unauthorized);
    }

    private static Result<LoginResponse> CreateBackoffResult(LoginAttemptDecision decision)
        => Result<LoginResponse>.Failure(
            "Too many failed login attempts. Please try again shortly.",
            ErrorCodes.TooManyRequests,
            decision.RetryAfter);
}
