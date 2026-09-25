using System.Text.RegularExpressions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Commands;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Auth.Commands;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<Result<string>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly Regex HanuEmailRegex = new(@"^[a-zA-Z0-9._%+-]+@hanu\.edu\.vn$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(e => HanuEmailRegex.IsMatch(e?.Trim() ?? ""))
            .WithMessage("Chỉ chấp nhận email có đuôi @hanu.edu.vn.");

        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");
    }
}

internal sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<string>>
{
    private readonly ISender _sender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private static readonly Regex StudentRegex = new(@"^\d+@hanu\.edu\.vn$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegisterCommandHandler(
        ISender sender,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IEmailSender emailSender)
    {
        _sender = sender;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        UserRole role;
        UserStatus status;
        string? studentId = null;

        if (StudentRegex.IsMatch(normalizedEmail))
        {
            role = UserRole.Student;
            status = UserStatus.Active;
            studentId = normalizedEmail.Split('@')[0];
        }
        else
        {
            role = UserRole.Teacher;
            status = UserStatus.Pending;
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var createResult = await _sender.Send(new CreateUserCommand(
            request.FullName,
            normalizedEmail,
            passwordHash,
            role,
            status,
            studentId), cancellationToken);

        if (!createResult.IsSuccess)
        {
            return Result<string>.Failure(createResult.Error ?? "Đăng ký không thành công.");
        }

        try
        {
            var otp = await _otpService.GenerateAndStoreOtpAsync(normalizedEmail);
            await _emailSender.SendOtpEmailAsync(normalizedEmail, otp, cancellationToken);
            return Result<string>.Success("Đăng ký tài khoản thành công. Vui lòng kiểm tra email để nhận mã OTP xác thực.");
        }
        catch (Exception)
        {
            return Result<string>.Success("Tài khoản đã được tạo thành công nhưng hệ thống gặp sự cố khi gửi mã OTP. Vui lòng bấm 'Gửi lại OTP' để nhận mã xác thực.");
        }
    }
}
