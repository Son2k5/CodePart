namespace CodePath.Application.Auth.Abstractions;

public interface IEmailSender
{
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default);
    Task SendPasswordChangedEmailAsync(string toEmail, CancellationToken ct = default);

    /// <summary>
    /// Sends a rendered notification email. Used by the notification module's email channel.
    /// Throws on delivery failure so the durable outbox worker can retry and eventually dead-letter.
    /// </summary>
    Task SendNotificationEmailAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default);
}
