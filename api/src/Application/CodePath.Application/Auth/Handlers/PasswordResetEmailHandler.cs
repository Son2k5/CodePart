using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Auth.Events;
using CodePath.Application.Notifications.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Auth.Handlers;

/// <summary>
/// Consumes <see cref="PasswordResetEmailNotification"/> from the durable outbox and hands the
/// reset link to <see cref="IEmailSender"/>. Failures propagate so the outbox worker retries
/// with backoff and eventually dead-letters — matching <c>EmailDispatchHandler</c>.
/// </summary>
public sealed class PasswordResetEmailHandler(
    INotificationDbContext db,
    IEmailSender emailSender,
    ILogger<PasswordResetEmailHandler> logger)
    : INotificationHandler<PasswordResetEmailNotification>
{
    public async Task Handle(PasswordResetEmailNotification e, CancellationToken ct)
    {
        var toEmail = await db.GetUserEmailAsync(e.RecipientId, ct);
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            // Permanent miss: log and complete so the worker does not dead-letter an undeliverable address.
            logger.LogWarning("Skipping password reset email for recipient {RecipientId}: no email address.", e.RecipientId);
            return;
        }

        await emailSender.SendPasswordResetEmailAsync(toEmail, e.Token, ct);
        logger.LogInformation("Password reset email sent to recipient {RecipientId}.", e.RecipientId);
    }
}
