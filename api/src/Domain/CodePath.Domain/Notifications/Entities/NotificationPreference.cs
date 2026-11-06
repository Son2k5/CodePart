using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Notifications.Entities;

/// <summary>
/// Per-user, per-type channel opt-in. Absence of a row means the channel defaults
/// (in-app on, email on) are applied by the notification service.
/// </summary>
public sealed class NotificationPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public bool InAppEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }

    private NotificationPreference() { }

    public static NotificationPreference Create(
        Guid userId,
        NotificationType type,
        bool inAppEnabled,
        bool emailEnabled,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            InAppEnabled = inAppEnabled,
            EmailEnabled = emailEnabled,
            CreatedAt = utcNow
        };
    }

    public void Update(bool inAppEnabled, bool emailEnabled, DateTime utcNow)
    {
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
        Touch(utcNow);
    }
}
