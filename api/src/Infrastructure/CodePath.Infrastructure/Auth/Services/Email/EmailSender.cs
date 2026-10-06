using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace CodePath.Infrastructure.Auth.Services.Email;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;
    private readonly IHostEnvironment _environment;
    private readonly SmtpOptions _options;
    private readonly AuthFrontendOptions _frontendOptions;

    public EmailSender(
        ILogger<EmailSender> logger,
        IHostEnvironment environment,
        IOptions<SmtpOptions> options,
        IOptions<AuthFrontendOptions> frontendOptions)
    {
        _logger = logger;
        _environment = environment;
        _options = options.Value;
        _frontendOptions = frontendOptions.Value;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            _logger.LogDebug("OTP email delivery suppressed outside production for {Recipient}.", toEmail);
            return;
        }

        var body = new BodyBuilder
        {
            TextBody = $"Your CodePath verification code is {otp}. It expires in 10 minutes.",
            HtmlBody = $"<p>Your CodePath verification code is:</p><p style=\"font-size:24px;font-weight:bold\">{otp}</p><p>It expires in 10 minutes.</p>"
        }.ToMessageBody();

        await SendAsync(toEmail, "CodePath email verification code", body, ct);
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetToken,
        CancellationToken ct = default)
    {
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            _logger.LogDebug("Password reset email delivery suppressed outside production for {Recipient}.", toEmail);
            return;
        }

        var resetUrl = $"{_frontendOptions.PublicBaseUrl.TrimEnd('/')}/reset-password#token={Uri.EscapeDataString(resetToken)}";
        var body = new BodyBuilder
        {
            TextBody = $"Use this link to reset your CodePath password: {resetUrl}. The link expires in 30 minutes.",
            HtmlBody = $"<p>A password reset was requested for your CodePath account.</p><p><a href=\"{resetUrl}\">Reset password</a></p><p>The link expires in 30 minutes. Ignore this email if you did not request it.</p>"
        }.ToMessageBody();

        await SendAsync(toEmail, "Reset your CodePath password", body, ct);
    }

    public async Task SendPasswordChangedEmailAsync(
        string toEmail,
        CancellationToken ct = default)
    {
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            _logger.LogDebug("Password change notification suppressed outside production for {Recipient}.", toEmail);
            return;
        }

        var body = new BodyBuilder
        {
            TextBody = "Your CodePath password was changed. If this was not you, contact support immediately.",
            HtmlBody = "<p>Your CodePath password was changed.</p><p>If this was not you, contact support immediately.</p>"
        }.ToMessageBody();

        await SendAsync(toEmail, "Your CodePath password was changed", body, ct);
    }

    private async Task SendAsync(
        string toEmail,
        string subject,
        MimeEntity body,
        CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = body;

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
        _logger.LogInformation("Authentication email delivered to {Recipient}.", toEmail);
    }
}
