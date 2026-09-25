namespace CodePath.Application.Auth.Abstractions;

public interface IEmailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
}
