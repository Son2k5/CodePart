using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Notifications.Entities;

/// <summary>
/// A single in-app notification delivered to one recipient. Persisted row is the
/// source of truth for the inbox, unread badge and the read/cleanup lifecycle.
/// </summary>
public sealed class Notification : BaseEntity
{
    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>Serialized JSON payload (courseId, topicId, submissionId, conversationId, ...).</summary>
    public string? Payload { get; private set; }

    /// <summary>
    /// Idempotency key (type + recipient + entity id). When present a unique index
    /// prevents duplicate deliveries on retry. Null means the notification is not deduplicated.
    /// </summary>
    public string? DedupKey { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid recipientId,
        NotificationType type,
        string title,
        string body,
        string? payloadJson,
        string? dedupKey,
        DateTime utcNow)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("Recipient id cannot be empty.", nameof(recipientId));

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Body = body,
            Payload = payloadJson,
            DedupKey = dedupKey,
            IsRead = false,
            CreatedAt = utcNow
        };
    }

    public bool MarkRead(DateTime utcNow)
    {
        if (IsRead)
            return false;

        IsRead = true;
        ReadAt = utcNow;
        Touch(utcNow);
        return true;
    }
}
