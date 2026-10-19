using System.Text.Json;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CodePath.Infrastructure.Learning;

public sealed class RagIndexWorker(IServiceScopeFactory scopes, ILogger<RagIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Tick(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("RAG worker will retry ({ErrorType}).", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
    internal async Task Tick(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IRagClient>();
        var now = DateTime.UtcNow;
        // Superseded revisions are never queried. Garbage collection removes only exact old revisions.
        var obsolete = await db.RagIndexJobs.AsNoTracking().Where(j => (j.Status == "Ready" || j.Status == "Failed") &&
            !db.Topics.Any(t => t.Id == j.TopicId && t.RowVersion == j.Revision && t.Status == TopicStatus.Published &&
                db.Chapters.Any(c => c.Id == t.ChapterId) && db.Classes.Any(c => c.Id == t.ClassId)))
            .Take(5).ToListAsync(ct);
        foreach (var old in obsolete)
        {
            await client.Delete(new(old.TopicId, old.Revision), ct);
            await db.RagIndexJobs.Where(j => j.Id == old.Id && (j.Status == "Ready" || j.Status == "Failed")).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Superseded"), ct);
        }
        var job = await db.RagIndexJobs.AsNoTracking().Where(j =>
            (j.Status == "Pending" || j.Status == "Processing") && j.NextAttemptAt <= now &&
            (j.LeaseUntil == null || j.LeaseUntil < now)).OrderBy(j => j.CreatedAt).FirstOrDefaultAsync(ct);
        if (job == null) return;
        var lease = Guid.NewGuid();
        var claimed = await db.RagIndexJobs.Where(j => j.Id == job.Id && (j.LeaseUntil == null || j.LeaseUntil < now) &&
            (j.Status == "Pending" || j.Status == "Processing")).ExecuteUpdateAsync(s =>
            s.SetProperty(j => j.Status, "Processing").SetProperty(j => j.LeaseToken, lease)
             .SetProperty(j => j.LeaseUntil, now.AddMinutes(5)).SetProperty(j => j.Attempts, j => j.Attempts + 1), ct);
        if (claimed != 1) return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(250));
            var t = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == job.TopicId &&
                t.RowVersion == job.Revision && t.Status == TopicStatus.Published && db.Chapters.Any(c => c.Id == t.ChapterId) &&
                db.Classes.Any(c => c.Id == t.ClassId), deadline.Token);
            var chunks = 0;
            var status = "Superseded";
            if (t != null)
            {
                var documents = new List<object>();
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
                foreach (var b in JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, LearningService.Json)!)
                {
                    if (b.Type == "Markdown") documents.Add(new { documentId = b.BlockId, b.BlockId,
                        title = t.Title, kind = "markdown", text = b.Data.GetProperty("markdown").GetString() });
                    else if (b.Type == "Pdf")
                    {
                        var assetId = b.Data.GetProperty("assetId").GetGuid();
                        var asset = await db.LearningAssets.SingleOrDefaultAsync(a => a.Id == assetId && a.Status == AssetStatus.Ready, deadline.Token);
                        if (asset == null) throw new RagIndexException("ASSET_NOT_READY");
                        documents.Add(new { documentId = assetId, b.BlockId, title = b.Data.GetProperty("title").GetString(),
                            kind = "pdf", url = await storage.SignedUrl(asset, deadline.Token) });
                    }
                }
                chunks = await client.Index(new(t.Id, t.ClassId, t.RowVersion, t.PublishedVersion, t.Title, t.Slug, t.PublishedContent), documents.ToArray(), deadline.Token);
                if (await db.Topics.AnyAsync(x => x.Id == t.Id && x.RowVersion == job.Revision && x.Status == TopicStatus.Published, ct)) status = "Ready";
            }
            if (status == "Superseded") await client.Delete(new(job.TopicId, job.Revision), ct);
            await db.RagIndexJobs.Where(j => j.Id == job.Id && j.LeaseToken == lease).ExecuteUpdateAsync(s =>
                s.SetProperty(j => j.Status, status).SetProperty(j => j.ChunkCount, chunks).SetProperty(j => j.ErrorCode, (string?)null)
                 .SetProperty(j => j.CompletedAt, DateTime.UtcNow).SetProperty(j => j.LeaseUntil, (DateTime?)null).SetProperty(j => j.LeaseToken, (Guid?)null), ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            var permanent = e is RagIndexException || job.Attempts >= 4;
            var error = e is RagIndexException ? e.Message : "INDEX_UNAVAILABLE";
            await db.RagIndexJobs.Where(j => j.Id == job.Id && j.LeaseToken == lease).ExecuteUpdateAsync(s =>
                s.SetProperty(j => j.Status, permanent ? "Failed" : "Pending").SetProperty(j => j.ErrorCode, error)
                 .SetProperty(j => j.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Min(300, 10 * Math.Pow(2, job.Attempts))))
                 .SetProperty(j => j.LeaseUntil, (DateTime?)null).SetProperty(j => j.LeaseToken, (Guid?)null), ct);
            logger.LogWarning("RAG job {JobId} failed: {Code}.", job.Id, error);
        }
    }
}
