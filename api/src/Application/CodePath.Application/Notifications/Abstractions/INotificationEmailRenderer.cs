using CodePath.Domain.Notifications.Enums;

namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Renders a notification into an HTML email body. Kept as an Application seam so the
/// email channel (which runs inside the outbox worker) stays independent of the concrete
/// templating approach implemented in Infrastructure. The subject/body are already sanitized
/// plain text; the implementation is responsible for producing safe, escaped HTML.
/// </summary>
public interface INotificationEmailRenderer
{
    string Render(NotificationType type, string subject, string body);
}
