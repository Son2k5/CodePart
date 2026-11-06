using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Common;

namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Application service that owns the notification lifecycle: validation, preference
/// resolution, dedup/idempotency, persistence and (in later phases) channel dispatch.
/// Other modules trigger notifications through domain/integration events that are
/// handled and translated into calls on this service.
/// </summary>
public interface INotificationService
{
    Task<Result<NotificationDto?>> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    Task<Result<int>> SendBulkAsync(BulkNotificationRequest request, CancellationToken cancellationToken = default);

    Task<Result<int>> BroadcastAsync(BroadcastRequest request, Guid actorId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<CursorPage<NotificationDto>> ListAsync(
        Guid recipientId,
        bool? isRead,
        NotificationType? type,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<Result<bool>> MarkAsReadAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken = default);

    Task<Result<int>> MarkAllAsReadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdatePreferencesAsync(Guid userId, IReadOnlyList<NotificationPreferenceUpdateDto> preferences, CancellationToken cancellationToken = default);
}
