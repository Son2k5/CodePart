using CodePath.Api.Extensions;
using CodePath.Application.Admin.Commands;
using CodePath.Application.Admin.Queries;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CodePath.Api.Endpoints;

public static partial class AdminEndpoints
{
    public sealed record BulkTeacherRequest(Guid[] UserIds);

    private static void MapTeacherManagement(RouteGroupBuilder group)
    {
        group.MapGet("/teachers/stats", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetTeacherStatsQuery(), ct)).ToHttpResult());
        group.MapGet("/faculties", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetFacultiesQuery(), ct)).ToHttpResult());
        group.MapGet("/teachers/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTeacherDetailQuery(id), ct);
            if (!result.IsSuccess) return result.ToHttpResult();
            var d = result.Value!;
            var p = d.Profile;
            return Results.Ok(new { p.Id, p.TeacherId, p.FullName, p.Email, p.Phone, p.Bio,
                p.FacultyId, p.FacultyName, p.Status, p.ClassesCount, p.CoursesCount,
                p.EmailVerifiedAt, p.CreatedAt, p.LastLoginAt, d.ManagedClasses, d.TeachingCourses });
        });
        group.MapPost("/teachers", async (CreateTeacherInput body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            var result = await sender.Send(new CreateTeacherCommand(actor.Value, body, GetClientIp(ctx)), ct);
            return result.IsSuccess ? Results.Created($"/api/admin/teachers/{result.Value}", new { id = result.Value }) : result.ToHttpResult();
        });
        group.MapPut("/teachers/{id:guid}", async (Guid id, TeacherProfileInput body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new UpdateTeacherCommand(actor.Value, id, body, GetClientIp(ctx)), ct)).ToHttpResult();
        });
        group.MapDelete("/teachers/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new DeleteTeacherCommand(actor.Value, id, GetClientIp(ctx)), ct)).ToHttpResult();
        });

        group.MapPost("/teachers/bulk-delete", async (BulkTeacherRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new BulkDeleteTeachersCommand(actor.Value, body.UserIds, GetClientIp(ctx)), ct)).ToHttpResult();
        });

        group.MapPost("/users/{id:guid}/enable", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new EnableUserCommand(actor.Value, id, GetClientIp(ctx)), ct)).ToHttpResult();
        });
        foreach (var approve in new[] { true, false })
        {
            group.MapPost(approve ? "/teachers/bulk-approve" : "/teachers/bulk-disable",
                async (BulkTeacherRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
                {
                    var actor = ctx.User.GetUserId();
                    if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
                    return (await sender.Send(new BulkTeachersCommand(actor.Value, body.UserIds, approve, GetClientIp(ctx)), ct)).ToHttpResult();
                });
        }
        group.MapGet("/teachers/export", async ([FromQuery] string? search, [FromQuery] Guid? facultyId,
            [FromQuery] UserStatus? status, [FromQuery] string? sortBy, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ExportTeachersQuery(new TeacherFilter(1, 100, search, facultyId, status, sortBy ?? "created_desc")), ct);
            return result.IsSuccess ? Results.File(result.Value!, "text/csv; charset=utf-8", "teachers.csv") : result.ToHttpResult();
        });
    }
}
