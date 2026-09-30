using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record VerifyOtpCommand(string Email, string Otp) : IRequest<Result<string>>;

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches(@"^\d{6}$")
            .WithMessage("OTP must contain exactly six digits.");
    }
}

internal sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<string>>
{
    private readonly IOtpService _otpService;
    private readonly IUsersDbContext _usersDbContext;
    private readonly TimeProvider _timeProvider;

    public VerifyOtpCommandHandler(
        IOtpService otpService,
        IUsersDbContext usersDbContext,
        TimeProvider timeProvider)
    {
        _otpService = otpService;
        _usersDbContext = usersDbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<string>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var (isValid, errorMessage) = await _otpService.VerifyOtpAsync(normalizedEmail, request.Otp);
        if (!isValid)
            return Result<string>.Failure(errorMessage ?? "The OTP is invalid.", ErrorCodes.BadRequest);

        var user = await _usersDbContext.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
            return Result<string>.Failure("User not found.", ErrorCodes.NotFound);

        user.VerifyEmail(_timeProvider.GetUtcNow().UtcDateTime);
        await _usersDbContext.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Email verified successfully. You can now sign in.");
    }
}
