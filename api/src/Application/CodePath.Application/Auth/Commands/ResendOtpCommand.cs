using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Queries;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record ResendOtpCommand(string Email) : IRequest<Result<string>>;

public sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

internal sealed class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Result<string>>
{
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly ISender _sender;

    public ResendOtpCommandHandler(IOtpService otpService, IEmailSender emailSender, ISender sender)
    {
        _otpService = otpService;
        _emailSender = emailSender;
        _sender = sender;
    }

    public async Task<Result<string>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var userResult = await _sender.Send(new GetUserByEmailQuery(normalizedEmail), cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            return Result<string>.Success("Nếu tài khoản tồn tại, mã OTP mới đã được gửi.");
        }

        if (userResult.Value.EmailVerifiedAt.HasValue)
        {
            return Result<string>.Failure("Email này đã được xác thực trước đó.");
        }

        var (canRequest, error) = await _otpService.CanRequestOtpAsync(normalizedEmail);
        if (!canRequest) return Result<string>.Failure(error!);

        var otp = await _otpService.GenerateAndStoreOtpAsync(normalizedEmail);
        await _emailSender.SendOtpEmailAsync(normalizedEmail, otp, cancellationToken);

        return Result<string>.Success("Mã OTP mới đã được gửi đến email của bạn.");
    }
}
