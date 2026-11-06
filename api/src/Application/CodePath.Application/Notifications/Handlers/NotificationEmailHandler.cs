using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodePath.Application.Notifications.Handlers;

/// <summary>
/// Email channel consumer. Runs inside the outbox worker scope: an <see cref="EmailDispatchNotification"/>
/// read from the outbox is turned into a rendered HTML email and handed to <see cref="IEmailSender"/>.
/// Any failure (unknown address, SMTP error) is allowed to propagate so the worker retries with
/// exponential backoff and eventually dead-letters the row — no Polly/inline retry needed here.
/// </summary>
public sealed class EmailDispatchHandler(
    INotificationDbContext db,
    INotificationEmailRenderer renderer,
    IEmailSender emailSender,
    ILogger<EmailDispatchHandler> logger)
    : INotificationHandler<EmailDispatchNotification>
{
    public async Task Handle(EmailDispatchNotification e, CancellationToken ct)
    {
        var toEmail = await db.GetUserEmailAsync(e.RecipientId, ct);
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            // Nothing to retry: the recipient has no deliverable address. Log and complete so
            // the worker marks the outbox row processed instead of dead-lettering a permanent miss.
            logger.LogWarning("Skipping notification email for recipient {RecipientId}: no email address.", e.RecipientId);
            return;
        }

        var html = renderer.Render(e.Type, e.Subject, e.Body);
        await emailSender.SendNotificationEmailAsync(toEmail, e.Subject, html, e.Body, ct);
        logger.LogInformation("Notification email ({Type}) queued to SMTP for recipient {RecipientId}.", e.Type, e.RecipientId);
    }
}
