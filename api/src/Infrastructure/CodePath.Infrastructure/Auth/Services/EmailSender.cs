using CodePath.Application.Auth.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    private readonly IHostEnvironment _env;

    public EmailSender(ILogger<EmailSender> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogDebug("[DEV-MOCK-EMAIL] Gửi mã OTP xác thực tới {ToEmail}: {Otp}", toEmail, otp);
        }
        else
        {
            _logger.LogInformation("Gửi mã OTP xác thực tới {ToEmail} thành công.", toEmail);
        }

        return Task.CompletedTask;
    }
}
