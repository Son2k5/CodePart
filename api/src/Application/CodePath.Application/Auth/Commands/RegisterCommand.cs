using System.Text.RegularExpressions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Auth.Commands;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<Result<string>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex HanuEmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@hanu\.edu\.vn$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(email => HanuEmailRegex.IsMatch(email?.Trim() ?? string.Empty))
            .WithMessage("Only @hanu.edu.vn email addresses are accepted.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .WithMessage("Password must contain between 12 and 128 characters.");
    }
}

internal sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<string>>
{
    private const string NeutralResponse =
        "If the account is eligible, a verification code will be sent to the supplied email address.";

    private static readonly Regex StudentRegex = new(
        @"^\d+@hanu\.edu\.vn$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IUsersDbContext _usersDbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RegisterCommandHandler(
        IUsersDbContext usersDbContext,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IEmailSender emailSender,
        ILogger<RegisterCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _usersDbContext = usersDbContext;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _emailSender = emailSender;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var existingUser = await _usersDbContext.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser?.EmailVerifiedAt is not null)
        {
            return Result<string>.Success(NeutralResponse);
        }

        // For an existing unverified account, reserve the OTP first. If an OTP is
        // active/cooling down or the hourly quota is exhausted, no account fields
        // (including password and role) are changed.
        if (existingUser is not null)
        {
            var existingIssue = await _otpService.TryIssueOtpAsync(normalizedEmail);
            if (!existingIssue.Issued || string.IsNullOrWhiteSpace(existingIssue.Otp))
            {
                return Result<string>.Success(NeutralResponse);
            }

            var updateResult = await CreateOrUpdateUserAsync(
                request,
                normalizedEmail,
                passwordHash,
                existingUser,
                cancellationToken);
            if (!updateResult.IsSuccess)
            {
                await _otpService.InvalidateOtpAsync(normalizedEmail, existingIssue.Otp);
                return Result<string>.Success(NeutralResponse);
            }

            return await SendOtpAsync(normalizedEmail, existingIssue.Otp, cancellationToken);
        }

        var createResult = await CreateOrUpdateUserAsync(
            request,
            normalizedEmail,
            passwordHash,
            existingUser: null,
            cancellationToken);
        if (!createResult.IsSuccess)
        {
            return Result<string>.Success(NeutralResponse);
        }

        var issue = await _otpService.TryIssueOtpAsync(normalizedEmail);
        if (!issue.Issued || string.IsNullOrWhiteSpace(issue.Otp))
        {
            return Result<string>.Success(NeutralResponse);
        }

        return await SendOtpAsync(normalizedEmail, issue.Otp, cancellationToken);
    }

    private async Task<Result<Guid>> CreateOrUpdateUserAsync(
        RegisterCommand request,
        string normalizedEmail,
        string passwordHash,
        User? existingUser,
        CancellationToken cancellationToken)
    {
        var isStudent = StudentRegex.IsMatch(normalizedEmail);
        var role = isStudent ? UserRole.Student : UserRole.Teacher;
        var studentId = isStudent ? normalizedEmail.Split('@')[0] : null;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            User user;
            if (existingUser is not null)
            {
                existingUser.UpdateUnverifiedAccount(request.FullName, passwordHash, role, studentId, utcNow);
                user = existingUser;
            }
            else
            {
                user = role == UserRole.Student
                    ? User.CreateStudent(request.FullName, normalizedEmail, passwordHash, studentId!, utcNow)
                    : User.CreateTeacher(request.FullName, normalizedEmail, passwordHash, utcNow);
                await _usersDbContext.AddAsync(user, cancellationToken);
            }

            await _usersDbContext.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(user.Id);
        }
        catch (ConflictException exception)
        {
            return Result<Guid>.Failure(exception.Message, ErrorCodes.Conflict);
        }
    }

    private async Task<Result<string>> SendOtpAsync(
        string normalizedEmail,
        string otp,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendOtpEmailAsync(normalizedEmail, otp, cancellationToken);
            return Result<string>.Success(NeutralResponse);
        }
        catch (Exception exception)
        {
            await _otpService.InvalidateOtpAsync(normalizedEmail, otp);
            _logger.LogError(exception, "OTP email delivery failed for {Recipient}.", normalizedEmail);
            return Result<string>.Failure(
                "The verification email service is temporarily unavailable. Please try again later.",
                ErrorCodes.InfraError);
        }
    }
}
