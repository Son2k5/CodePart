namespace CodePath.Application.Auth.Abstractions;

public interface IEmailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default);
    Task SendPasswordChangedEmailAsync(string toEmail, CancellationToken ct = default);
}
