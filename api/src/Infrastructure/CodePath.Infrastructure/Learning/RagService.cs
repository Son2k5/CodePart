using System.Text.Json;
using CodePath.Application.Ai;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Learning;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
namespace CodePath.Infrastructure.Learning;

public sealed partial class RagService(AppDbContext db, ILearningRagAccess access, IRagClient client,
    IFileStorage storage, IConnectionMultiplexer redis, IConfiguration config, ILogger<RagService> logger,
    IServiceProvider? services = null) : IRagService
{
    public static RagReference Reference(RagPassage p) => new(p.ChunkId, p.DocumentId, p.BlockId, p.TopicId,
        p.ClassId, p.Version, p.Title, p.Page, p.Heading);
    private bool Enabled => config.GetValue<bool>("Rag:Enabled");
    private static void ValidateQuestion(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 2000) throw new AiPolicyException("INVALID_CONTEXT");
    }
    private async Task Limit(Actor actor)
    {
        var now = DateTime.UtcNow;
        var result = (int)await redis.GetDatabase().ScriptEvaluateAsync("""
            local d=tonumber(redis.call('GET',KEYS[1]) or '0')
            local m=tonumber(redis.call('GET',KEYS[2]) or '0')
            if d>=50 or m>=5 then return 0 end
            redis.call('INCR',KEYS[1]); redis.call('EXPIRE',KEYS[1],172800)
            redis.call('INCR',KEYS[2]); redis.call('EXPIRE',KEYS[2],120)
            return 1
            """, [$"rag:daily:{actor.Id}:{now:yyyyMMdd}", $"rag:minute:{actor.Id}:{now:yyyyMMddHHmm}"]);
        if (result == 0) throw new AiPolicyException("RATE_LIMIT", 429);
    }
    public async Task<object> Status(Guid id, Actor a, CancellationToken ct)
    {
        var t = await access.RagTopic(id, a, true, ct);
        var job = await db.RagIndexJobs.AsNoTracking().SingleOrDefaultAsync(j => j.TopicId == id && j.Revision == t.Revision, ct);
        return new { enabled = Enabled, topicId = id, version = t.Version, status = job?.Status ?? "NotIndexed",
            chunks = job?.ChunkCount ?? 0, errorCode = job?.ErrorCode, attempts = job?.Attempts ?? 0, completedAt = job?.CompletedAt };
    }
    public async Task<object> Reindex(Guid id, Actor a, CancellationToken ct)
    {
        var t = await access.RagTopic(id, a, true, ct);
        if (!Enabled) throw new AiPolicyException("RAG_DISABLED", 503);
        if (t.Version < 1) throw new AiPolicyException("PUBLISH_REQUIRED", 409);
        var job = await db.RagIndexJobs.SingleOrDefaultAsync(j => j.TopicId == id && j.Revision == t.Revision, ct);
        if (job == null) db.RagIndexJobs.Add(new() { TopicId = id, Revision = t.Revision });
        else if (job.LeaseUntil == null || job.LeaseUntil <= DateTime.UtcNow)
        { job.Status = "Pending"; job.Attempts = 0; job.ErrorCode = null; job.NextAttemptAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return await Status(id, a, ct);
    }
    private async Task<RagPassage[]> Retrieve(string query, RagTopic[] topics, Actor a, CancellationToken ct)
    {
        var scopes = new List<RagScope>();
        foreach (var t in topics)
            if (await db.RagIndexJobs.AnyAsync(j => j.TopicId == t.Id && j.Revision == t.Revision && j.Status == "Ready", ct))
                scopes.Add(new(t.Id, t.Revision));
        if (scopes.Count == 0) return [];
        var result = await client.Search(query, scopes.ToArray(), ct);
        if (result.Passages == null || result.Passages.Length > 8 || result.Passages.Any(p =>
            p.Content == null || p.Content.Length > 6000 || !scopes.Contains(new RagScope(p.TopicId, p.Revision))))
            throw new InvalidDataException("Invalid retrieval scope.");
        return await StillAllowed(result.Passages, a, ct) ? result.Passages : [];
    }
    public async Task<RagPassage[]> ForProblem(Guid problemId, string query, Actor a, CancellationToken ct)
    {
        if (!Enabled) return [];
        try
        {
            var topics = new List<RagTopic>();
            foreach (var id in await access.RagProblemTopics(problemId, a, ct))
            {
                try { topics.Add(await access.RagTopic(id, a, false, ct)); }
                catch (DomainRuleViolationException) { /* Locked and revoked topics never enter retrieval. */ }
            }
            return await Retrieve(query[..Math.Min(query.Length, 4000)], topics.ToArray(), a, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException)
        { logger.LogWarning("RAG hint retrieval unavailable; using existing hint pipeline."); return []; }
    }
    public async Task<bool> StillAllowed(RagPassage[] passages, Actor a, CancellationToken ct)
    {
        foreach (var group in passages.GroupBy(p => p.TopicId))
        {
            RagTopic t;
            try { t = await access.RagTopic(group.Key, a, a.IsAdmin || a.IsTeacher, ct); }
            catch (DomainRuleViolationException) { return false; }
            var blocks = JsonSerializer.Deserialize<ContentBlock[]>(t.Content, LearningService.Json)!;
            foreach (var p in group)
            {
                var block = blocks.SingleOrDefault(b => b.BlockId == p.BlockId);
                if (p.Revision != t.Revision || p.ClassId != t.ClassId || p.Version != t.Version || block == null) return false;
                if (block.Type == "Pdf")
                {
                    var assetId = block.Data.GetProperty("assetId").GetGuid();
                    if (p.DocumentId != assetId || !await db.LearningAssets.AnyAsync(x => x.Id == assetId && x.Status == AssetStatus.Ready, ct)) return false;
                }
                else if (block.Type != "Markdown" || p.DocumentId != block.BlockId) return false;
            }
        }
        return true;
    }
    public async Task<RagPassage[]> Search(Guid id, string question, Actor a, CancellationToken ct)
    {
        ValidateQuestion(question);
        var t = await access.RagTopic(id, a, a.IsAdmin || a.IsTeacher, ct);
        if (!Enabled) throw new AiPolicyException("RAG_DISABLED", 503);
        await Limit(a);
        try { return await Retrieve(question, [t], a, ct); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or OperationCanceledException)
        { throw new AiPolicyException("RAG_UNAVAILABLE", 503); }
    }
    public async Task<RagAskResult> Ask(Guid id, string question, Actor a, CancellationToken ct)
    {
        var passages = await Search(id, question, a, ct);
        if (passages.Length == 0) return new("Chưa có học liệu phù hợp đã được xử lý cho câu hỏi này.", [], "insufficient_context");
        RagAnswer answer;
        try { answer = await client.Answer(question, passages, ct); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or OperationCanceledException or JsonException)
        { throw new AiPolicyException("AI_UNAVAILABLE", 503); }
        if (!await StillAllowed(passages, a, ct)) throw new AiPolicyException("CONTEXT_CHANGED", 409);
        var ids = answer.SourceIds ?? [];
        if (answer.InsufficientContext || ids.Length == 0 || ids.Any(id => !passages.Any(p => p.ChunkId.ToString() == id)))
            return new("Chưa tìm thấy đủ căn cứ trong học liệu để trả lời câu hỏi này.", [], "insufficient_context");
        return new(answer.Answer, passages.Where(p => ids.Contains(p.ChunkId.ToString())).Select(Reference).ToArray(), "sources_provided");
    }
    public async Task<object> Source(Guid id, Guid blockId, int version, Actor a, CancellationToken ct)
    {
        var t = await access.RagTopic(id, a, a.IsAdmin || a.IsTeacher, ct);
        if (t.Version != version) throw new AiPolicyException("SOURCE_CHANGED", 409);
        var b = JsonSerializer.Deserialize<ContentBlock[]>(t.Content, LearningService.Json)!.SingleOrDefault(b => b.BlockId == blockId);
        if (b == null) throw new AiPolicyException("NOT_FOUND", 404);
        if (b.Type == "Markdown") return new { kind = "markdown", title = t.Title, text = b.Data.GetProperty("markdown").GetString() };
        if (b.Type != "Pdf") throw new AiPolicyException("NOT_FOUND", 404);
        var assetId = b.Data.GetProperty("assetId").GetGuid();
        var asset = await db.LearningAssets.SingleOrDefaultAsync(x => x.Id == assetId && x.Status == AssetStatus.Ready, ct);
        if (asset == null) throw new AiPolicyException("NOT_FOUND", 404);
        return new { kind = "pdf", title = b.Data.GetProperty("title").GetString(), url = await storage.SignedUrl(asset, ct) };
    }
}
