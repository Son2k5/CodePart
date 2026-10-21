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
    public sealed record BulkStudentRequest(Guid[] UserIds);

    private static void MapStudentManagement(RouteGroupBuilder group)
    {
        group.MapGet("/students/stats", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetStudentStatsQuery(), ct)).ToHttpResult());

        group.MapGet("/students", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? facultyId,
            [FromQuery] UserStatus? status,
            [FromQuery] string? sortBy,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new StudentFilter(page ?? 1, pageSize ?? 20, search, facultyId, status, sortBy ?? "created_desc");
            return (await sender.Send(new GetStudentsPagedQuery(filter), ct)).ToHttpResult();
        });

        group.MapGet("/students/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStudentDetailQuery(id), ct);
            if (!result.IsSuccess) return result.ToHttpResult();
            var d = result.Value!;
            var p = d.Profile;
            return Results.Ok(new { p.Id, p.StudentId, p.FullName, p.Email, p.Phone, p.Bio,
                p.FacultyId, p.FacultyName, p.Status, p.ClassesCount, p.CoursesCount,
                p.EmailVerifiedAt, p.CreatedAt, p.LastLoginAt, d.EnrolledClasses, d.EnrolledCourses });
        });

        group.MapPost("/students", async (CreateStudentInput body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            var result = await sender.Send(new CreateStudentCommand(actor.Value, body, GetClientIp(ctx)), ct);
            return result.IsSuccess ? Results.Created($"/api/admin/students/{result.Value}", new { id = result.Value }) : result.ToHttpResult();
        });

        group.MapPut("/students/{id:guid}", async (Guid id, StudentProfileInput body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new UpdateStudentCommand(actor.Value, id, body, GetClientIp(ctx)), ct)).ToHttpResult();
        });

        group.MapDelete("/students/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new DeleteStudentCommand(actor.Value, id, GetClientIp(ctx)), ct)).ToHttpResult();
        });

        group.MapPost("/students/bulk-delete", async (BulkStudentRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var actor = ctx.User.GetUserId();
            if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
            return (await sender.Send(new BulkDeleteStudentsCommand(actor.Value, body.UserIds, GetClientIp(ctx)), ct)).ToHttpResult();
        });

        foreach (var approve in new[] { true, false })
        {
            group.MapPost(approve ? "/students/bulk-approve" : "/students/bulk-disable",
                async (BulkStudentRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
                {
                    var actor = ctx.User.GetUserId();
                    if (actor is null) return Result<object>.Failure("Bạn cần đăng nhập để thực hiện thao tác này.", ErrorCodes.Unauthorized).ToHttpResult();
                    return (await sender.Send(new BulkStudentsCommand(actor.Value, body.UserIds, approve, GetClientIp(ctx)), ct)).ToHttpResult();
                });
        }

        group.MapGet("/students/export", async ([FromQuery] string? search, [FromQuery] Guid? facultyId,
            [FromQuery] UserStatus? status, [FromQuery] string? sortBy, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ExportStudentsQuery(new StudentFilter(1, 100, search, facultyId, status, sortBy ?? "created_desc")), ct);
            return result.IsSuccess ? Results.File(result.Value!, "text/csv; charset=utf-8", "students.csv") : result.ToHttpResult();
        });
    }
}
