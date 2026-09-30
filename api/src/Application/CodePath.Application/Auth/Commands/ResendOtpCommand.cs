using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Auth.Commands;

public sealed record ResendOtpCommand(string Email) : IRequest<Result<string>>;

public sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

internal sealed class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Result<string>>
{
    private const string NeutralResponse =
        "If the account is eligible, a verification code will be sent to the supplied email address.";

    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly IUsersDbContext _usersDbContext;
    private readonly ILogger<ResendOtpCommandHandler> _logger;

    public ResendOtpCommandHandler(
        IOtpService otpService,
        IEmailSender emailSender,
        IUsersDbContext usersDbContext,
        ILogger<ResendOtpCommandHandler> logger)
    {
        _otpService = otpService;
        _emailSender = emailSender;
        _usersDbContext = usersDbContext;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var user = await _usersDbContext.GetByEmailReadOnlyAsync(normalizedEmail, cancellationToken);
        if (user is null || user.EmailVerifiedAt.HasValue)
        {
            return Result<string>.Success(NeutralResponse);
        }

        var issue = await _otpService.TryIssueOtpAsync(normalizedEmail);
        if (!issue.Issued || string.IsNullOrWhiteSpace(issue.Otp))
        {
            return Result<string>.Success(NeutralResponse);
        }

        try
        {
            await _emailSender.SendOtpEmailAsync(normalizedEmail, issue.Otp, cancellationToken);
            return Result<string>.Success(NeutralResponse);
        }
        catch (Exception exception)
        {
            await _otpService.InvalidateOtpAsync(normalizedEmail, issue.Otp);
            _logger.LogError(exception, "OTP resend email delivery failed for {Recipient}.", normalizedEmail);
            return Result<string>.Failure(
                "The verification email service is temporarily unavailable. Please try again later.",
                ErrorCodes.InfraError);
        }
    }
}
