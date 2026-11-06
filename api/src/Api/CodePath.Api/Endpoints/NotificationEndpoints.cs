using CodePath.Api.Extensions;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Notifications.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CodePath.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .RequireAuthorization("ActiveUser")
            .WithTags("Notifications");

        group.MapGet("/", async (
            HttpContext http,
            INotificationService service,
            [FromQuery] bool? isRead,
            [FromQuery] NotificationType? type,
            [FromQuery] string? cursor,
            [FromQuery] int take,
            CancellationToken ct) =>
        {
            var recipientId = http.User.GetUserId()!.Value;
            var page = await service.ListAsync(recipientId, isRead, type, cursor, take, ct);
            return Results.Ok(page);
        });

        group.MapGet("/unread-count", async (HttpContext http, INotificationService service, CancellationToken ct) =>
        {
            var recipientId = http.User.GetUserId()!.Value;
            var count = await service.GetUnreadCountAsync(recipientId, ct);
            return Results.Ok(new { count });
        });

        group.MapPatch("/{id:guid}/read", async (Guid id, HttpContext http, INotificationService service, CancellationToken ct) =>
        {
            var recipientId = http.User.GetUserId()!.Value;
            return (await service.MarkAsReadAsync(recipientId, id, ct)).ToHttpResult();
        });

        group.MapPatch("/read-all", async (HttpContext http, INotificationService service, CancellationToken ct) =>
        {
            var recipientId = http.User.GetUserId()!.Value;
            return (await service.MarkAllAsReadAsync(recipientId, ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, INotificationService service, CancellationToken ct) =>
        {
            var recipientId = http.User.GetUserId()!.Value;
            return (await service.DeleteAsync(recipientId, id, ct)).ToHttpResult();
        });

        group.MapGet("/preferences", async (HttpContext http, INotificationService service, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId()!.Value;
            var preferences = await service.GetPreferencesAsync(userId, ct);
            return Results.Ok(preferences);
        });

        group.MapPut("/preferences", async (
            NotificationPreferenceUpdateDto[] body,
            HttpContext http,
            INotificationService service,
            CancellationToken ct) =>
        {
            var userId = http.User.GetUserId()!.Value;
            return (await service.UpdatePreferencesAsync(userId, body, ct)).ToHttpResult();
        }).RequireRateLimiting("notifications-write");

        return app;
    }
}
