using CodePath.Application.Auth.Abstractions;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    public EmailSender(ILogger<EmailSender> logger) => _logger = logger;

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK-EMAIL] Gửi mã OTP xác thực tới {ToEmail}: {Otp}", toEmail, otp);
        return Task.CompletedTask;
    }
}
