using CodePath.Api.Extensions;
using CodePath.Application.Ai;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Shared.Kernel.Common;
namespace CodePath.Api.Endpoints;

public static class RagEndpoints
{
    private static Actor Actor(HttpContext h) => new(h.User.GetUserId()!.Value, h.User.IsInRole("Admin"), h.User.IsInRole("Teacher"));
    private static async Task<Microsoft.AspNetCore.Http.IResult> Execute(Func<Task<object>> action)
    {
        try { return Results.Ok(await action()); }
        catch (AiPolicyException e) { return Result<object>.Failure(ErrorCatalog.GetSafeMessage(e.Code), e.Code).ToHttpResult(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Result<object>.Failure("Dữ liệu đã bị thay đổi bởi thao tác khác. Vui lòng tải lại trang.", ErrorCodes.Conflict).ToHttpResult(); }
    }
    public static void MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/learning/classes", async (HttpContext h, ILearningRagAccess s, CancellationToken ct) =>
            Results.Ok(await s.RagClasses(Actor(h), ct))).RequireAuthorization("ActiveUser");
        var read = app.MapGroup("/api/topics/{id:guid}/rag").RequireAuthorization("ActiveUser").WithTags("Learning RAG");
        read.MapPost("/ask", (Guid id, RagAskInput input, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(async () => await s.Ask(id, input.Question, Actor(h), ct)));
        read.MapPost("/search", (Guid id, RagAskInput input, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(async () => await s.Search(id, input.Question, Actor(h), ct)));
        read.MapGet("/sources/{blockId:guid}", (Guid id, Guid blockId, int version, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.Source(id, blockId, version, Actor(h), ct)));
        read.MapGet("/status", (Guid id, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.Status(id, Actor(h), ct))).RequireAuthorization("CanManageClassContent");
        read.MapPost("/reindex", (Guid id, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.Reindex(id, Actor(h), ct))).RequireAuthorization("CanManageClassContent");
        var drafts = read.MapGroup("/drafts").RequireAuthorization("CanManageClassContent");
        drafts.MapGet("", (Guid id, HttpContext h, IRagService s, CancellationToken ct) => Execute(() => s.Drafts(id, Actor(h), ct)));
        drafts.MapPost("", (Guid id, RagDraftInput input, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.GenerateDraft(id, input, Actor(h), ct)));
        drafts.MapPut("/{draftId:guid}", (Guid id, Guid draftId, RagDraftEdit input, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.EditDraft(id, draftId, input, Actor(h), ct)));
        drafts.MapPost("/{draftId:guid}/verify", (Guid id, Guid draftId, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.VerifyDraft(id, draftId, Actor(h), ct)));
        drafts.MapPost("/{draftId:guid}/review", (Guid id, Guid draftId, RagDraftReview input, HttpContext h, IRagService s, CancellationToken ct) =>
            Execute(() => s.ReviewDraft(id, draftId, input.RowVersion, Actor(h), ct)));
    }
}
