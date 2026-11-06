using CodePath.Api.Extensions;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;

namespace CodePath.Api.Endpoints;

public static class AdminNotificationEndpoints
{
    public static IEndpointRouteBuilder MapAdminNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/notifications")
            .RequireAuthorization("AdminOnly")
            .WithTags("Notifications (Admin)");

        group.MapPost("/broadcast", async (
            BroadcastRequest body,
            HttpContext http,
            INotificationService service,
            CancellationToken ct) =>
        {
            var actorId = http.User.GetUserId()!.Value;
            var ipAddress = http.Connection?.RemoteIpAddress?.ToString();
            return (await service.BroadcastAsync(body, actorId, ipAddress, ct)).ToHttpResult();
        }).RequireRateLimiting("notifications-write");

        return app;
    }
}
