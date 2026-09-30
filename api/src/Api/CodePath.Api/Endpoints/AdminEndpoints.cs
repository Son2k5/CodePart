using CodePath.Api.Extensions;
using CodePath.Application.Admin.Commands;
using CodePath.Application.Admin.Queries;
using CodePath.Shared.Kernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CodePath.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/teachers", async (
            [FromQuery] UserStatus? status,
            [FromQuery] string? cursor,
            [FromQuery] int? limit,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListTeachersQuery(status, cursor, NormalizeLimit(limit)),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("ListTeachers")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/users/{id:guid}/approve", async (
            Guid id,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.User.GetUserId();
            if (actorId is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new ApproveTeacherCommand(actorId.Value, id, GetClientIp(httpContext)),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("ApproveTeacher")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/users/{id:guid}/reject", async (
            Guid id,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.User.GetUserId();
            if (actorId is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new RejectTeacherCommand(actorId.Value, id, GetClientIp(httpContext)),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("RejectTeacher")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/users/{id:guid}/disable", async (
            Guid id,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.User.GetUserId();
            if (actorId is null)
                return Results.Unauthorized();

            var result = await sender.Send(
                new DisableUserCommand(actorId.Value, id, GetClientIp(httpContext)),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("DisableUser")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/audit-logs", async (
            [FromQuery] Guid? targetUserId,
            [FromQuery] string? cursor,
            [FromQuery] int? limit,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListAuditLogsQuery(targetUserId, cursor, NormalizeLimit(limit)),
                cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("ListAuditLogs")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static int NormalizeLimit(int? limit) => limit is null or 0 ? 20 : limit.Value;

    private static string? GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();
}
