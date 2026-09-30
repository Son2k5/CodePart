using CodePath.Api.Contracts;
using CodePath.Api.Extensions;
using CodePath.Application.Exercises.Commands;
using CodePath.Application.Exercises.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CodePath.Api.Endpoints;

public static class ExerciseEndpoints
{
    public static IEndpointRouteBuilder MapExerciseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exercises").WithTags("Exercises");

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetExerciseQuery(slug), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetExercise")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{exerciseId:guid}/run", async (
            Guid exerciseId,
            RunCodeRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RunCodeCommand(
                exerciseId, request.Language, request.SourceCode, request.CustomInput), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("RunExerciseCode")
        .RequireAuthorization("ActiveStudent")
        .RequireRateLimiting("code-execution-rate-limit")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/{exerciseId:guid}/submit", async (
            Guid exerciseId,
            SubmitCodeRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var studentId = httpContext.User.GetUserId();
            if (studentId is null)
                return Results.Unauthorized();

            var result = await sender.Send(new SubmitCodeCommand(
                studentId.Value, exerciseId, request.Language, request.SourceCode), cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("SubmitExerciseCode")
        .RequireAuthorization("ActiveStudent")
        .RequireRateLimiting("code-execution-rate-limit")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
