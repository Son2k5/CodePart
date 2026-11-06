using System.Security.Claims;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace CodePath.Api.Endpoints;

/// <summary>
/// Realtime delivery endpoint for the Notification module. Authenticated clients connect to
/// /hubs/notifications (JWT is supplied via the access_token query string, see Program.cs) and
/// are placed in a per-user group <c>user:{userId}</c> so the outbox worker can push new
/// notifications without tracking connection ids.
/// </summary>
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId), Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(userId), Context.ConnectionAborted);

        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(string userId) => $"user:{userId}";
}

/// <summary>
/// SignalR-backed <see cref="IRealtimeNotifier"/>. Registered after the infrastructure no-op so
/// it wins single-service resolution. Delivery is targeted by user id (SignalR's per-user
/// tracking) and also to the explicit <c>user:{id}</c> group.
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    private const string EventName = "notification";

    public Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        // Clients.User resolves the per-user connection set maintained by the hub's group join;
        // sending only here avoids duplicate delivery (the user's connection is also a group member).
        return hub.Clients.User(recipientId.ToString()).SendAsync(EventName, notification, cancellationToken);
    }
}
