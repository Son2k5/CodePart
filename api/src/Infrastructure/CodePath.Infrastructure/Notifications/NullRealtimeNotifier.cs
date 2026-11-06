using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IRealtimeNotifier"/> used when no SignalR-backed notifier is registered
/// (e.g. unit tests or realtime disabled). Kept last in the DI registration order so a later
/// API-layer registration wins.
/// </summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
