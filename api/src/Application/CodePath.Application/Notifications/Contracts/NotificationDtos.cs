using System.Text.Json;
using System.Text.Json.Serialization;
using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Notifications.Contracts;

/// <summary>
/// Command DTO used by any module (through events or the service) to deliver a notification.
/// </summary>
public sealed record NotificationRequest(
    Guid RecipientId,
    NotificationType Type,
    string Title,
    string Body,
    Guid? EntityId = null,
    IReadOnlyDictionary<string, object?>? Payload = null,
    /// <summary>When false, the notification is in-app only and never emailed (e.g. transient OTP).</summary>
    bool AllowEmail = true);

/// <summary>Bulk send to an explicit recipient set (e.g. an entire class).</summary>
public sealed record BulkNotificationRequest(
    IReadOnlyList<Guid> RecipientIds,
    NotificationType Type,
    string Title,
    string Body,
    Guid? EntityId = null,
    IReadOnlyDictionary<string, object?>? Payload = null,
    bool AllowEmail = true);

/// <summary>Admin broadcast scopes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BroadcastScope>))]
public enum BroadcastScope
{
    AllUsers = 0,
    Role = 1,
    Class = 2
}

public sealed record BroadcastRequest(
    string Title,
    string Body,
    BroadcastScope Scope = BroadcastScope.AllUsers,
    UserRole? Role = null,
    Guid? ClassId = null);

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    JsonElement? Payload,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

public sealed record NotificationPreferenceDto(
    NotificationType Type,
    bool InAppEnabled,
    bool EmailEnabled);

public sealed record NotificationPreferenceUpdateDto(
    NotificationType Type,
    bool InAppEnabled,
    bool EmailEnabled);

/// <summary>Cursor-paginated result set.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

/// <summary>
/// Projection row returned to the scheduled deadline scanner. <see cref="EffectiveDueAt"/> is the
/// topic due date with any per-student extension applied; <see cref="Completed"/> is true when the
/// student has already finished the topic (they are then skipped by the worker).
/// </summary>
public sealed record DeadlineCandidate(
    Guid StudentId,
    Guid TopicId,
    Guid ClassId,
    string Title,
    DateTime EffectiveDueAt,
    bool Completed);
