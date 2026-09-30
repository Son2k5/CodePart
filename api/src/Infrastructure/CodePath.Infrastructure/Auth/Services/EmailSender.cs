using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace CodePath.Infrastructure.Auth.Services;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    private readonly IHostEnvironment _environment;
    private readonly SmtpOptions _options;

    public EmailSender(
        ILogger<EmailSender> logger,
        IHostEnvironment environment,
        IOptions<SmtpOptions> options)
    {
        _logger = logger;
        _environment = environment;
        _options = options.Value;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            _logger.LogDebug("OTP email delivery suppressed outside production for {Recipient}.", toEmail);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "CodePath email verification code";
        message.Body = new BodyBuilder
        {
            TextBody = $"Your CodePath verification code is {otp}. It expires in 10 minutes.",
            HtmlBody = $"<p>Your CodePath verification code is:</p><p style=\"font-size:24px;font-weight:bold\">{otp}</p><p>It expires in 10 minutes.</p>"
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = Enum.TryParse<SecureSocketOptions>(_options.SocketOptions, true, out var parsed)
            ? parsed
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, ct);
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        _logger.LogInformation("OTP email delivered to {Recipient}.", toEmail);
    }
}
