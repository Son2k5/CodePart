using CodePath.Api.Extensions;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using Microsoft.AspNetCore.Mvc;
namespace CodePath.Api.Endpoints;
public static partial class LearningEndpoints
{
    private static Actor Actor(HttpContext ctx) => new(ctx.User.GetUserId()!.Value, ctx.User.IsInRole("Admin"), ctx.User.IsInRole("Teacher"));
    public sealed record ModeBody(ClassMode Mode);
    public sealed record DateBody(DateTime? DueAt);
    public sealed record ScheduleBody(DateTime PublishAt);
    public static IEndpointRouteBuilder MapLearningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapLearningExtras();
        app.MapRagEndpoints();
        app.MapCurriculumEndpoints();
        var read = app.MapGroup("/api").RequireAuthorization("ActiveUser").RequireAuthorization(p => p.RequireAuthenticatedUser()).WithTags("Learning");
        read.MapGet("/classes/{id:guid}/outline", async (Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Outline(id, Actor(h), ct)).ToHttpResult());
        read.MapGet("/classes/{id:guid}/topics/{slug}", async (Guid id, string slug, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Read(id, slug, Actor(h), ct)).ToHttpResult());
        read.MapPost("/topics/{id:guid}/progress/heartbeat", async (Guid id, HeartbeatInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Progress(id, body, Actor(h), ct)).ToHttpResult()).RequireRateLimiting("learning-heartbeat");
        read.MapPost("/topics/{id:guid}/complete", async (Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Progress(id, null, Actor(h), ct)).ToHttpResult());
        read.MapGet("/classes/{id:guid}/my-progress", async (Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Report(id, Actor(h).Id, 1, 1, Actor(h), ct)).ToHttpResult());
        foreach (var action in new[] { "badges", "streak", "continue-learning" })
        { var captured = action; read.MapGet("/me/" + action, async (HttpContext h, ILearningService s, CancellationToken ct) => (await s.Personal(captured, Actor(h), ct)).ToHttpResult()); }
        var manage = app.MapGroup("/api").RequireAuthorization("CanManageClassContent").WithTags("Learning Management");
        read.MapGet("/classes/{classId:guid}/chapters", async (Guid classId, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Chapters(classId, null, "list", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{classId:guid}/chapters", async (Guid classId, ChapterInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Chapters(classId, null, "create", body, null, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/classes/{classId:guid}/chapters/{id:guid}", async (Guid classId, Guid id, ChapterInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Chapters(classId, id, "update", body, null, Actor(h), ct)).ToHttpResult());
        manage.MapDelete("/classes/{classId:guid}/chapters/{id:guid}", async (Guid classId, Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Chapters(classId, id, "delete", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPatch("/classes/{classId:guid}/chapters/reorder", async (Guid classId, ReorderInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Chapters(classId, null, "reorder", null, body, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{classId:guid}/topics", async (Guid classId, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, null, "list", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{classId:guid}/topics", async (Guid classId, TopicInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, null, "create", body, null, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{classId:guid}/topics/{id:guid}", async (Guid classId, Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, id, "get", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/classes/{classId:guid}/topics/{id:guid}", async (Guid classId, Guid id, TopicInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, id, "update", body, null, Actor(h), ct)).ToHttpResult());
        manage.MapDelete("/classes/{classId:guid}/topics/{id:guid}", async (Guid classId, Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, id, "delete", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPost("/classes/{classId:guid}/topics/{id:guid}/duplicate", async (Guid classId, Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, id, "duplicate", null, null, Actor(h), ct)).ToHttpResult());
        manage.MapPatch("/classes/{classId:guid}/topics/reorder", async (Guid classId, ReorderInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Topics(classId, null, "reorder", null, body, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/topics/{id:guid}/draft", async (Guid id, DraftInput body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, "draft", body, Actor(h), ct)).ToHttpResult());
        foreach (var action in new[] { "publish", "unpublish" }) { var captured = action; manage.MapPost("/topics/{id:guid}/" + action, async (Guid id, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, captured, null, Actor(h), ct)).ToHttpResult()); }
        manage.MapPost("/topics/{id:guid}/schedule", async (Guid id, ScheduleBody body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, "schedule", body.PublishAt, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/topics/{id:guid}/completion-rule", async (Guid id, CompletionRule body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, "completion-rule", body, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/topics/{id:guid}/due-date", async (Guid id, DateBody body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, "due-date", body.DueAt, Actor(h), ct)).ToHttpResult());
        manage.MapPut("/topics/{id:guid}/exercises", async (Guid id, ExerciseInput[] body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Edit(id, "exercises", body, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{id:guid}/progress-report", async (Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Report(id, null, page ?? 1, pageSize ?? 20, Actor(h), ct)).ToHttpResult());
        manage.MapGet("/classes/{id:guid}/students/{studentId:guid}/progress", async (Guid id, Guid studentId, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Report(id, studentId, 1, 1, Actor(h), ct)).ToHttpResult());
        app.MapPatch("/api/admin/classes/{id:guid}/mode", async (Guid id, ModeBody body, HttpContext h, ILearningService s, CancellationToken ct) => (await s.Mode(id, body.Mode, Actor(h), ct)).ToHttpResult()).RequireAuthorization("AdminOnly");
        return app;
    }
}
