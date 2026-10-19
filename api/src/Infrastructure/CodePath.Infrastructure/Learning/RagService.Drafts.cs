using System.Text.Json;
using CodePath.Application.Ai;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CodePath.Infrastructure.Learning;

public sealed partial class RagService
{
    public static void ValidateDraft(TeachingDraftContent c, string kind)
    {
        if (c == null || string.IsNullOrWhiteSpace(c.Title) || c.Title.Length > 250 || string.IsNullOrWhiteSpace(c.Description) ||
            c.Description.Length > 12000 || c.ReferenceSolution == null || c.ReferenceSolution.Length > 20000 ||
            c.Tests == null || c.Quiz == null || c.SourceIds is not { Length: > 0 and <= 8 }) throw new AiPolicyException("INVALID_DRAFT");
        if (kind == "exercise" && (string.IsNullOrWhiteSpace(c.ReferenceSolution) || c.Tests.Length is < 10 or > 15 ||
            c.Tests.Count(t => t != null && t.IsSample) is < 1 or > 2 || c.Quiz.Length != 0 || c.Tests.Any(t =>
                t == null || t.Input == null || t.ExpectedOutput == null || t.Input.Length > 8000 || t.ExpectedOutput.Length > 8000)))
            throw new AiPolicyException("INVALID_DRAFT");
        if (kind == "quiz" && (c.Tests.Length != 0 || c.ReferenceSolution.Length != 0 || c.Quiz.Length is < 3 or > 5 || c.Quiz.Any(q =>
            q == null || string.IsNullOrWhiteSpace(q.Question) || q.Question.Length > 2000 || q.Choices is not { Length: 4 } ||
            q.Choices.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 2000) || q.CorrectIndex is < 0 or > 3 ||
            string.IsNullOrWhiteSpace(q.Explanation) || q.Explanation.Length > 2000))) throw new AiPolicyException("INVALID_DRAFT");
    }
    public async Task<object> GenerateDraft(Guid topicId, RagDraftInput input, Actor actor, CancellationToken ct)
    {
        var t = await access.RagTopic(topicId, actor, true, ct);
        if (input.Kind is not ("exercise" or "quiz") || input.Language is not ("python" or "cpp" or "java" or "csharp"))
            throw new AiPolicyException("INVALID_CONTEXT");
        var passages = await Search(topicId, input.Question, actor, ct);
        if (passages.Length == 0) throw new AiPolicyException("INSUFFICIENT_CONTEXT", 409);
        TeachingDraftContent content;
        try { content = await client.Draft(input.Question, input.Kind, input.Language, passages, ct); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or OperationCanceledException or JsonException)
        { throw new AiPolicyException("DRAFT_GENERATION_FAILED", 503); }
        ValidateDraft(content, input.Kind);
        if (!await StillAllowed(passages, actor, ct) || content.SourceIds.Any(id => !passages.Any(p => p.ChunkId.ToString() == id)))
            throw new AiPolicyException("CONTEXT_CHANGED", 409);
        var draft = new RagTeachingDraft { TopicId = topicId, SourceRevision = t.Revision, CreatedBy = actor.Id,
            Kind = input.Kind, Language = input.Language, Content = JsonSerializer.Serialize(content, LearningService.Json),
            Sources = JsonSerializer.Serialize(passages.Where(p => content.SourceIds.Contains(p.ChunkId.ToString())).Select(Reference), LearningService.Json) };
        db.Set<RagTeachingDraft>().Add(draft); await db.SaveChangesAsync(ct); return draft;
    }
    public async Task<object> Drafts(Guid topicId, Actor actor, CancellationToken ct)
    {
        await access.RagTopic(topicId, actor, true, ct);
        return await db.Set<RagTeachingDraft>().AsNoTracking().Where(d => d.TopicId == topicId)
            .OrderByDescending(d => d.CreatedAt).Take(30).ToListAsync(ct);
    }
    private async Task<RagTeachingDraft> Draft(Guid topicId, Guid draftId, Actor actor, CancellationToken ct)
    {
        var t = await access.RagTopic(topicId, actor, true, ct);
        var d = await db.Set<RagTeachingDraft>().SingleOrDefaultAsync(d => d.Id == draftId && d.TopicId == topicId, ct);
        if (d == null) throw new AiPolicyException("NOT_FOUND", 404);
        if (d.SourceRevision != t.Revision) throw new AiPolicyException("CONTEXT_CHANGED", 409);
        return d;
    }
    public async Task<object> EditDraft(Guid topicId, Guid draftId, RagDraftEdit input, Actor actor, CancellationToken ct)
    {
        var draft = await Draft(topicId, draftId, actor, ct);
        if (draft.RowVersion != input.RowVersion) throw new AiPolicyException("CONFLICT", 409);
        ValidateDraft(input.Content, draft.Kind);
        var sources = JsonSerializer.Deserialize<RagReference[]>(draft.Sources, LearningService.Json)!;
        if (input.Content.SourceIds.Any(id => !sources.Any(s => s.ChunkId.ToString() == id))) throw new AiPolicyException("INVALID_DRAFT");
        draft.Content = JsonSerializer.Serialize(input.Content, LearningService.Json);
        draft.Status = "Draft"; draft.Validation = "NotRun"; draft.ReviewedBy = null; draft.ReviewedAt = null;
        draft.RowVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct); return draft;
    }
    public async Task<object> VerifyDraft(Guid topicId, Guid draftId, Actor actor, CancellationToken ct)
    {
        var draft = await Draft(topicId, draftId, actor, ct);
        if (draft.Kind != "exercise") throw new AiPolicyException("INVALID_CONTEXT");
        var executor = services?.GetService<ICodeExecutor>();
        if (executor == null || !executor.Supports(draft.Language)) throw new AiPolicyException("JUDGE_UNAVAILABLE", 503);
        await Limit(actor);
        var key = $"rag:verify:{actor.Id}"; var token = Guid.NewGuid().ToString(); var cache = redis.GetDatabase();
        if (!await cache.StringSetAsync(key, token, TimeSpan.FromMinutes(4), StackExchange.Redis.When.NotExists))
            throw new AiPolicyException("RATE_LIMIT", 429);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(3));
            var content = JsonSerializer.Deserialize<TeachingDraftContent>(draft.Content, LearningService.Json)!;
            ValidateDraft(content, "exercise");
            var passed = 0;
            foreach (var test in content.Tests)
            {
                var result = await executor.Execute(draft.Language, content.ReferenceSolution, test.Input, new(2000, 128), deadline.Token);
                if (result.Verdict != Verdict.Accepted || !JudgeRules.Equal(test.ExpectedOutput, result.Output ?? "", ComparisonMode.Lines)) break;
                passed++;
            }
            // Do not accept verification against content edited while execution was in flight.
            var latest = await access.RagTopic(topicId, actor, true, ct);
            if (latest.Revision != draft.SourceRevision) throw new AiPolicyException("CONTEXT_CHANGED", 409);
            draft.Validation = passed == content.Tests.Length ? "Passed" : $"Failed:{passed}/{content.Tests.Length}";
            draft.Status = "Draft"; draft.ReviewedBy = null; draft.ReviewedAt = null; draft.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(ct); return draft;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AiPolicyException("JUDGE_TIMEOUT", 503); }
        finally
        {
            try { await cache.ScriptEvaluateAsync("if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) else return 0 end", [key], [token]); }
            catch (StackExchange.Redis.RedisException) { /* Lease expires. */ }
        }
    }
    public async Task<object> ReviewDraft(Guid topicId, Guid draftId, Guid rowVersion, Actor actor, CancellationToken ct)
    {
        var draft = await Draft(topicId, draftId, actor, ct);
        if (draft.RowVersion != rowVersion) throw new AiPolicyException("CONFLICT", 409);
        if (draft.Kind == "exercise" && draft.Validation != "Passed") throw new AiPolicyException("VERIFY_REQUIRED", 409);
        draft.Status = "Reviewed"; draft.ReviewedBy = actor.Id; draft.ReviewedAt = DateTime.UtcNow; draft.RowVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct); return draft;
    }
}
