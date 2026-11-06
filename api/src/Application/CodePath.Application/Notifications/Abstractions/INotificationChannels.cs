using CodePath.Application.Notifications.Contracts;

namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Outbound seam for the realtime channel. The infrastructure/API layer binds this to a
/// SignalR hub; a no-op implementation keeps the pipeline functional when realtime is disabled
/// (e.g. unit tests). Delivery is best-effort: callers must not fail notification persistence
/// when a push throws.
/// </summary>
public interface IRealtimeNotifier
{
    Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache for a user's unread notification count, backed by Redis. A miss returns null so the
/// service can fall back to a database count. All operations are best-effort.
/// </summary>
public interface IUnreadCountCache
{
    Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetAsync(Guid userId, int count, CancellationToken cancellationToken = default);

    Task IncrementAsync(Guid userId, int delta, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
