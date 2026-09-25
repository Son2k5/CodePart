using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Commands;
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
        RuleFor(x => x.Otp).NotEmpty().Length(6).Matches(@"^\d{6}$").WithMessage("Mã OTP gồm 6 chữ số.");
    }
}

internal sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<string>>
{
    private readonly IOtpService _otpService;
    private readonly ISender _sender;

    public VerifyOtpCommandHandler(IOtpService otpService, ISender sender)
    {
        _otpService = otpService;
        _sender = sender;
    }

    public async Task<Result<string>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var (isValid, errorMessage) = await _otpService.VerifyOtpAsync(normalizedEmail, request.Otp);
        if (!isValid) return Result<string>.Failure(errorMessage ?? "Mã OTP không hợp lệ.");

        var verifyResult = await _sender.Send(new VerifyUserEmailCommand(normalizedEmail), cancellationToken);
        if (!verifyResult.IsSuccess) return Result<string>.Failure(verifyResult.Error ?? "Không thể xác minh email.");

        return Result<string>.Success("Xác minh email thành công. Bây giờ bạn có thể đăng nhập.");
    }
}
