using CodePath.Api.Extensions;
using CodePath.Api.Middleware;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;

namespace CodePath.Api.Endpoints;

public static class JudgeEndpoints
{
    private static Actor Actor(HttpContext c) => new(c.User.GetUserId()!.Value, c.User.IsInRole("Admin"), c.User.IsInRole("Teacher"));
    public static void MapJudgeEndpoints(this IEndpointRouteBuilder app)
    {
        var student = app.MapGroup("/api/problems").WithTags("Code Judge").RequireAuthorization("ActiveStudent");
        student.MapGet("/", async (HttpContext c, IJudgeService s, CancellationToken ct, int page = 1, Guid? courseId = null, Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null) => (await s.ListProblems(Actor(c), false, page, ct, courseId, classId, library, search, difficulty, category, language)).ToHttpResult())
            .WithMetadata(new SkipActivityTrackingAttribute());
        student.MapGet("/scopes", async (HttpContext c, IJudgeAccess s, CancellationToken ct) => Results.Ok(await s.Scopes(Actor(c), false, ct)));
        student.MapGet("/{id:guid}", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.GetProblem(id, Actor(c), false, ct)).ToHttpResult());
        student.MapPost("/{id:guid}/run", async (Guid id, CodeRequest r, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.Run(id, r, Actor(c), ct)).ToHttpResult())
            .RequireRateLimiting("judge-run");
        student.MapPost("/{id:guid}/submit", async (Guid id, CodeRequest r, HttpContext c, IJudgeService s, CancellationToken ct) => {
            var result = await s.Submit(id, r, Actor(c), ct);
            return result.IsSuccess ? Results.Json(result.Value, statusCode: 202) : result.ToHttpResult();
        }).RequireRateLimiting("judge-submit");
        student.MapGet("/{id:guid}/submissions", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct, int page = 1, int pageSize = 20) =>
            (await s.History(Actor(c), false, id, null, null, null, page, pageSize, ct)).ToHttpResult())
            .WithMetadata(new SkipActivityTrackingAttribute());
        app.MapGet("/api/submissions/{id:guid}", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) =>
            (await s.GetSubmission(id, Actor(c), false, ct)).ToHttpResult()).RequireAuthorization("ActiveStudent")
            .WithMetadata(new SkipActivityTrackingAttribute());
        var admin = app.MapGroup("/api/admin/problems").WithTags("Code Judge Management").RequireAuthorization("JudgeManager");
        admin.MapGet("/", async (HttpContext c, IJudgeService s, CancellationToken ct, int page = 1, Guid? courseId = null, Guid? classId = null) => (await s.ListProblems(Actor(c), true, page, ct, courseId, classId)).ToHttpResult());
        admin.MapGet("/scopes", async (HttpContext c, IJudgeAccess s, CancellationToken ct) => Results.Ok(await s.Scopes(Actor(c), true, ct)));
        admin.MapGet("/{id:guid}", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.GetProblem(id, Actor(c), true, ct)).ToHttpResult());
        admin.MapPost("/", async (ProblemWrite r, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.SaveProblem(null, r, Actor(c), ct)).ToHttpResult());
        admin.MapPut("/{id:guid}", async (Guid id, ProblemWrite r, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.SaveProblem(id, r, Actor(c), ct)).ToHttpResult());
        admin.MapDelete("/{id:guid}", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.ChangeProblem(id, "delete", Actor(c), ct)).ToHttpResult());
        foreach (var action in new[] { "publish", "unpublish" })
        {
            var captured = action;
            admin.MapPost("/{id:guid}/" + action, async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.ChangeProblem(id, captured, Actor(c), ct)).ToHttpResult());
        }
        // Atomic replacement supports add/edit/delete/reorder and bulk JSON import.
        admin.MapPut("/{id:guid}/tests", async (Guid id, List<TestWrite> tests, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.SaveTests(id, tests, Actor(c), ct)).ToHttpResult());
        admin.MapPost("/{id:guid}/tests/import", async (Guid id, List<TestWrite> tests, HttpContext c, IJudgeService s, CancellationToken ct) => (await s.SaveTests(id, tests, Actor(c), ct)).ToHttpResult());
        app.MapGet("/api/admin/submissions", async (HttpContext c, IJudgeService s, CancellationToken ct, Guid? problemId = null, Guid? courseId = null, Guid? userId = null, Verdict? verdict = null, int page = 1, int pageSize = 20, Guid? classId = null) =>
            (await s.History(Actor(c), true, problemId, courseId, userId, verdict, page, pageSize, ct, classId)).ToHttpResult()).RequireAuthorization("JudgeManager");
        app.MapGet("/api/admin/submissions/{id:guid}", async (Guid id, HttpContext c, IJudgeService s, CancellationToken ct) =>
            (await s.GetSubmission(id, Actor(c), true, ct)).ToHttpResult()).RequireAuthorization("JudgeManager");
    }
}
