using CodePath.Api.Extensions;
using CodePath.Application.Teaching.Commands;
using CodePath.Application.Teaching.Dtos;
using CodePath.Application.Teaching.Queries;
using CodePath.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CodePath.Api.Endpoints;

public static class TeacherEndpoints
{
    public sealed record StatusRequest(string Status);
    public sealed record StudentRequest(string Email);
    public sealed record JoinRequest(string JoinCode);
    public sealed record GradeRequest(decimal Score, string? Feedback);
    private static Guid Actor(HttpContext ctx) => ctx.User.GetUserId() ?? throw new UnauthorizedAppException("Authentication required.");

    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var teacher = app.MapGroup("/api/teacher").WithTags("Teacher").RequireAuthorization("ActiveTeacher");
        teacher.MapGet("/courses", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCoursesQuery(Actor(ctx), true), ct)).ToHttpResult());
        teacher.MapPost("/courses", async (CreateCourseInput body, HttpContext ctx, ISender sender, CancellationToken ct) => {
            var result = await sender.Send(new CreateCourseCommand(Actor(ctx), body), ct);
            return result.IsSuccess ? Results.Created($"/api/teacher/courses/{result.Value}", new { id = result.Value }) : result.ToHttpResult();
        });
        teacher.MapGet("/courses/{id:guid}", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCourseQuery(Actor(ctx), id), ct)).ToHttpResult());
        teacher.MapPut("/courses/{id:guid}/status", async (Guid id, StatusRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SetCourseStatusCommand(Actor(ctx), id, body.Status), ct)).ToHttpResult());
        teacher.MapGet("/courses/{id:guid}/students", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCourseStudentsQuery(Actor(ctx), id), ct)).ToHttpResult());
        teacher.MapPost("/courses/{id:guid}/students", async (Guid id, StudentRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AddCourseStudentCommand(Actor(ctx), id, body.Email), ct)).ToHttpResult());
        teacher.MapDelete("/courses/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RemoveCourseStudentCommand(Actor(ctx), id, studentId), ct)).ToHttpResult());
        teacher.MapGet("/courses/{id:guid}/exercises", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCourseExercisesQuery(Actor(ctx), id, true), ct)).ToHttpResult());
        teacher.MapPost("/courses/{id:guid}/exercises", async (Guid id, CreateExerciseInput body, HttpContext ctx, ISender sender, CancellationToken ct) => {
            var result = await sender.Send(new CreateCourseExerciseCommand(Actor(ctx), id, body), ct);
            return result.IsSuccess ? Results.Created($"/api/teacher/courses/{id}/exercises", new { id = result.Value }) : result.ToHttpResult();
        });
        teacher.MapGet("/courses/{id:guid}/submissions", async (Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetSubmissionsQuery(Actor(ctx), id, page ?? 1, pageSize ?? 20), ct)).ToHttpResult());
        teacher.MapGet("/courses/{id:guid}/submissions/{submissionId:guid}", async (Guid id, Guid submissionId, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetSubmissionQuery(Actor(ctx), id, submissionId), ct)).ToHttpResult());
        teacher.MapPut("/courses/{id:guid}/submissions/{submissionId:guid}/grade", async (Guid id, Guid submissionId, GradeRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GradeSubmissionCommand(Actor(ctx), id, submissionId, body.Score, body.Feedback), ct)).ToHttpResult());
        teacher.MapGet("/classes", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetClassesQuery(Actor(ctx)), ct)).ToHttpResult());
        teacher.MapGet("/classes/{id:guid}/students", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetClassStudentsQuery(Actor(ctx), id), ct)).ToHttpResult());

        var student = app.MapGroup("/api/student/courses").WithTags("Student Courses").RequireAuthorization("ActiveStudent");
        student.MapGet("/", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCoursesQuery(Actor(ctx), false), ct)).ToHttpResult());
        student.MapPost("/join", async (JoinRequest body, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new JoinCourseCommand(Actor(ctx), body.JoinCode), ct)).ToHttpResult()).RequireRateLimiting("auth-rate-limit");
        student.MapGet("/{id:guid}/exercises", async (Guid id, HttpContext ctx, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCourseExercisesQuery(Actor(ctx), id, false), ct)).ToHttpResult());
        return app;
    }
}
