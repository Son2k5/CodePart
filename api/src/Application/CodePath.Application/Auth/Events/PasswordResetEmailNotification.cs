using CodePath.Application.Notifications.Events;

namespace CodePath.Application.Auth.Events;

/// <summary>
/// Staged in <c>outbox_messages</c> by the forgot-password flow instead of sending SMTP inline.
/// The outbox worker re-publishes it through MediatR so delivery gets retry, exponential
/// backoff and dead-lettering for free. The token is a self-validating Data Protection
/// payload (15-minute lifetime); it is already encrypted, so persisting it in the outbox
/// exposes no usable secret.
/// </summary>
public sealed record PasswordResetEmailNotification(
    Guid RecipientId, string Token) : IntegrationEvent;
