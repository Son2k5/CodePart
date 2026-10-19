using CodePath.Api.Extensions;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using Microsoft.AspNetCore.Mvc;
namespace CodePath.Api.Endpoints;
public static partial class LearningEndpoints
{
    public sealed record AiDraftInput(int Count, string Difficulty, string[] Languages);
    private static void MapLearningExtras(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api").RequireAuthorization("ActiveUser").RequireAuthorization(p => p.RequireAuthenticatedUser());
        var manage = app.MapGroup("/api").RequireAuthorization("CanManageClassContent");
        read.MapGet("/classes/{id:guid}/search", async (Guid id, string q, [FromQuery] int? page, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Search(id, q, page ?? 1, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{id:guid}/score-config", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Scores(id, "get-config", null, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/classes/{id:guid}/score-config", async (Guid id, ScoreConfiguration body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Scores(id, "put-config", body, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{id:guid}/scores", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Scores(id, "list", null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{id:guid}/scores/recompute", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Scores(id, "recompute", null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{id:guid}/scores/adjust", async (Guid id, AdjustmentInput body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Scores(id, "adjust", body, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/topics/{id:guid}/extensions", async (Guid id, ExtensionInput body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Extend(id, body, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{id:guid}/clone-from", async (Guid id, CloneInput body, [FromHeader(Name = "Idempotency-Key")] string key, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Clone(id, body, key, Actor(h), ct)).ToHttpResult()).RequireRateLimiting("learning-write");
        manage.MapGet("/topics/{id:guid}/upstream-status", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Upstream(id, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{id:guid}/progress-report/export", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => { var result = await s.Export(id, Actor(h), ct); return result.IsSuccess ? Results.File(result.Value!, "text/csv; charset=utf-8", "progress.csv") : result.ToHttpResult(); });
        manage.MapPost("/assets/sign-upload", async (UploadRequest body, HttpContext h, ILearningAssets s, CancellationToken ct) => (await s.Sign(body, Actor(h), ct)).ToHttpResult()).RequireRateLimiting("learning-write");
        manage.MapPost("/assets/{id:guid}/complete", async (Guid id, HttpContext h, ILearningAssets s, CancellationToken ct) => (await s.Complete(id, Actor(h), ct)).ToHttpResult());
        read.MapGet("/assets/{id:guid}", async (Guid id, HttpContext h, ILearningAssets s, CancellationToken ct) => (await s.Get(id, Actor(h), ct)).ToHttpResult());
        read.MapGet("/topics/{id:guid}/comments", async (Guid id, [FromQuery] int? page, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Comments(id, null, "list", null, page ?? 1, Actor(h), ct)).ToHttpResult());
        read.MapPost("/topics/{id:guid}/comments", async (Guid id, CommentInput body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Comments(id, null, "create", body, 1, Actor(h), ct)).ToHttpResult()).RequireRateLimiting("learning-write");
        read.MapPatch("/comments/{id:guid}", async (Guid id, CommentInput body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Comments(Guid.Empty, id, "edit", body, 1, Actor(h), ct)).ToHttpResult()).RequireRateLimiting("learning-write");
        read.MapDelete("/comments/{id:guid}", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Comments(Guid.Empty, id, "delete", null, 1, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/comments/{id:guid}/pin", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Comments(Guid.Empty, id, "pin", null, 1, Actor(h), ct)).ToHttpResult());
        read.MapGet("/topics/{id:guid}/notes", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Notes(id, "get", null, Actor(h), ct)).ToHttpResult());
        read.MapPut("/topics/{id:guid}/notes", async (Guid id, NoteInput body, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Notes(id, "put", body, Actor(h), ct)).ToHttpResult());
        read.MapDelete("/topics/{id:guid}/notes", async (Guid id, HttpContext h, ILearningExtras s, CancellationToken ct) => (await s.Notes(id, "delete", null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/topics/{id:guid}/exercises/ai-draft", async (Guid id, AiDraftInput body, ITopicExerciseDraftGenerator s, CancellationToken ct) => (await s.GenerateAsync(id, body.Count, body.Difficulty, body.Languages, ct)).ToHttpResult());
    }
}
