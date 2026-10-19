using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CodePath.Infrastructure.Learning;
public sealed partial class LearningService : ILearningExtras
{
    public async Task<Result<object>> Search(Guid classId, string query, int page, Actor a, CancellationToken ct)
    {
        await Access(classId, a, false, ct);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200) Reject("CONTENT_INVALID", "Query must have 1–200 characters.");
        page = Math.Clamp(page, 1, 100000);
        // MySQL full-text uses the accent-insensitive collation on Document. All user text is parameterized.
        var matches = await db.TopicSearch.FromSqlInterpolated($"SELECT * FROM learning_search WHERE ClassId = {classId} AND MATCH(Document) AGAINST ({query} IN NATURAL LANGUAGE MODE) ORDER BY MATCH(Document) AGAINST ({query} IN NATURAL LANGUAGE MODE) DESC").AsNoTracking().ToListAsync(ct);
        var topics = await Ordered(classId, ct); var done = await db.TopicProgress.Where(p => p.UserId == a.Id && p.ClassId == classId && p.Status == LearningStatus.Completed).Select(p => p.TopicId).ToListAsync(ct);
        var allowed = topics.Where((t, i) => t.LockedReason(Now, i == 0 || done.Contains(topics[i - 1].Id)) == null).ToDictionary(t => t.Id);
        var visible = matches.Where(m => allowed.ContainsKey(m.TopicId)).ToList();
        return Ok(new { total = visible.Count, page, items = visible.Skip((page - 1) * 20).Take(20).Select(m => new { m.TopicId, allowed[m.TopicId].Title, allowed[m.TopicId].Slug, snippet = m.Document[..Math.Min(m.Document.Length, 240)] }) });
    }
    private async Task Recompute(Guid classId, Guid? topic, Guid? user, CancellationToken ct)
    {
        var affected = await judge.Submissions.Where(s => s.ClassId == classId && s.TopicId != null && s.FinishedAt != null && (!topic.HasValue || s.TopicId == topic) && (!user.HasValue || s.UserId == user))
            .Select(s => new { s.UserId, TopicId = s.TopicId!.Value }).Distinct().ToListAsync(ct);
        foreach (var item in affected) await Judged(new(Guid.Empty, item.UserId, item.TopicId), ct);
    }
    public async Task<Result<object>> Scores(Guid id, string action, object? input, Actor a, CancellationToken ct)
    {
        var c = await Access(id, a, true, ct);
        if (c.Mode != ClassMode.Managed) Reject("CLASS_MODE_NOT_SUPPORTED", "SelfStudy has no gradebook.");
        var config = await db.ClassScoreConfigs.FindAsync([id], ct);
        if (action == "get-config") return Ok(config == null ? new ScoreConfiguration(new(), []) : JsonSerializer.Deserialize<ScoreConfiguration>(config.Configuration, Json)!);
        if (action == "put-config")
        {
            var value = (ScoreConfiguration)input!; var p = value.LatePolicy;
            if (p == null || !Enum.IsDefined(p.Mode) || p.PercentPerDay is < 0 or > 100 || p.FixedPercent is < 0 or > 100 || p.MaxPenaltyPercent is < 0 or > 100 || p.GracePeriodMinutes < 0 || p.HardCloseAfterDays < 0 ||
                p.Tiers?.Any(t => t.AfterDays < 0 || t.Percent is < 0 or > 100) == true || value.Categories == null || value.Categories.Any(c => string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 100 || c.Weight < 0 || c.TopicIds == null) || value.Categories.Length > 0 && value.Categories.Sum(c => c.Weight) != 100)
                Reject("CONTENT_INVALID", "Invalid score configuration; category weights must total 100.");
            var ids = value.Categories!.SelectMany(c => c.TopicIds).ToArray();
            if (ids.Distinct().Count() != ids.Length || await db.Topics.CountAsync(t => t.ClassId == id && ids.Contains(t.Id), ct) != ids.Length) Reject("CONTENT_INVALID", "Categories must reference distinct topics in this class.");
            if (config == null) { config = new() { ClassId = id }; db.ClassScoreConfigs.Add(config); }
            config.Configuration = JsonSerializer.Serialize(value, Json); await Save(id, a, "ScoreConfig", id, ct);
            await Recompute(id, null, null, ct); return Ok(value);
        }
        if (action == "recompute") { await Recompute(id, null, null, ct); await Save(id, a, "Recompute", id, ct); return Ok(new { recomputed = true }); }
        if (action == "adjust")
        {
            var r = (AdjustmentInput)input!;
            if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 2000 || r.IdempotencyKey == Guid.Empty || r.Points is < -10000 or > 10000) Reject("CONTENT_INVALID", "A reason and idempotency key are required.");
            if (!await db.ClassEnrollments.AnyAsync(e => e.ClassId == id && e.StudentId == r.UserId && e.Status == ClassEnrollmentStatus.Active, ct)) Reject("NOT_ENROLLED", "Student is not enrolled.");
            var old = await db.ScoreEntries.FindAsync([r.UserId, "Adjustment", r.IdempotencyKey], ct);
            if (old != null) { if (old.ClassId != id || old.EffectivePoints != r.Points) Reject("CONFLICT", "Idempotency key reused."); return Ok(old); }
            var entry = new ScoreEntry { UserId = r.UserId, ClassId = id, Source = "Adjustment", SourceRefId = r.IdempotencyKey, BasePoints = r.Points, EffectivePoints = r.Points };
            db.ScoreEntries.Add(entry); await Save(id, a, "Adjustment: " + ContentValidator.Plain(r.Reason), r.UserId, ct); return Ok(entry);
        }
        var scores = await db.ScoreEntries.AsNoTracking().Where(s => s.ClassId == id).ToListAsync(ct);
        var categories = config == null ? [] : JsonSerializer.Deserialize<ScoreConfiguration>(config.Configuration, Json)!.Categories;
        return Ok(new { entries = scores, totals = scores.GroupBy(s => s.UserId).Select(g => new { userId = g.Key, total = g.Sum(s => s.EffectivePoints),
            categories = categories.Select(c => new { c.Name, c.Weight, points = g.Where(s => c.TopicIds.Contains(s.TopicId)).Sum(s => s.EffectivePoints) }) }) });
    }
    public async Task<Result<object>> Extend(Guid id, ExtensionInput r, Actor a, CancellationToken ct)
    {
        var t = await Topic(id, ct); if (t == null) Reject("NOT_FOUND", "Topic not found.");
        var c = await Access(t!.ClassId, a, true, ct);
        if (c.Mode != ClassMode.Managed) Reject("DEADLINE_NOT_ALLOWED", "SelfStudy has no deadlines.");
        if (r.NewDueAt.Kind != DateTimeKind.Utc || t.DueAt == null || r.NewDueAt <= t.DueAt || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 2000) Reject("CONTENT_INVALID", "Extension must be later than the topic deadline and include a reason.");
        if (!await db.ClassEnrollments.AnyAsync(e => e.ClassId == c.Id && e.StudentId == r.UserId && e.Status == ClassEnrollmentStatus.Active, ct)) Reject("NOT_ENROLLED", "Student is not enrolled.");
        var extension = await db.DeadlineExtensions.FindAsync([r.UserId, id], ct);
        if (extension == null) { extension = new() { UserId = r.UserId, TopicId = id }; db.DeadlineExtensions.Add(extension); }
        extension.NewDueAt = r.NewDueAt; extension.Reason = ContentValidator.Plain(r.Reason); extension.GrantedBy = a.Id;
        await Save(c.Id, a, "Extension", r.UserId, ct); await Recompute(c.Id, id, r.UserId, ct); return Ok(extension);
    }
    public async Task<Result<object>> Upstream(Guid id, Actor a, CancellationToken ct)
    {
        var t = await Topic(id, ct); if (t == null) Reject("NOT_FOUND", "Topic not found."); await Access(t!.ClassId, a, true, ct);
        var source = t.SourceTopicId.HasValue ? await db.Topics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == t.SourceTopicId, ct) : null;
        return Ok(new { t.SourceTopicId, t.SourcePublishedVersion, latestVersion = source?.PublishedVersion, hasUpdate = source != null && source.PublishedVersion > t.SourcePublishedVersion });
    }
    public Task<Result<object>> Clone(Guid targetId, CloneInput r, string key, Actor a, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(() => CloneCore(targetId, r, key, a, ct));
    private async Task<Result<object>> CloneCore(Guid targetId, CloneInput r, string key, Actor a, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); judge.ChangeTracker.Clear();
        await Access(targetId, a, true, ct);
        if (targetId == r.SourceClassId || string.IsNullOrWhiteSpace(key) || key.Length > 100 || r.ChapterIds != null && r.TopicIds != null) Reject("CONTENT_INVALID", "Invalid clone selection or idempotency key.");
        var source = await db.Classes.SingleOrDefaultAsync(c => c.Id == r.SourceClassId, ct);
        if (source == null || !(source.IsTemplate || source.Mode == ClassMode.SelfStudy && source.AccessType == ClassAccessType.Public)) Reject("FORBIDDEN", "Source is not a template.");
        if (source!.IsTemplate) await TemplateAccess(source, a, false, ct);
        if (r.ChapterIds != null && (r.ChapterIds.Length == 0 || r.ChapterIds.Distinct().Count() != r.ChapterIds.Length ||
            await db.Chapters.CountAsync(ch => ch.ClassId == r.SourceClassId && r.ChapterIds.Contains(ch.Id), ct) != r.ChapterIds.Length))
            Reject("CONTENT_INVALID", "Invalid source chapters.");
        if (r.TopicIds != null && (r.TopicIds.Length == 0 || r.TopicIds.Distinct().Count() != r.TopicIds.Length ||
            await db.Topics.CountAsync(t => t.ClassId == r.SourceClassId && r.TopicIds.Contains(t.Id), ct) != r.TopicIds.Length))
            Reject("CONTENT_INVALID", "Invalid source topics.");
        var topics = (await Ordered(r.SourceClassId, ct)).Where(t => t.Status == TopicStatus.Published && (t.PublishAt == null || t.PublishAt <= Now) &&
            (r.ChapterIds == null || r.ChapterIds.Contains(t.ChapterId)) && (r.TopicIds == null || r.TopicIds.Contains(t.Id))).ToList();
        var adminIds = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
        if (!source.IsTemplate && topics.Any(t => !adminIds.Contains(t.CreatedBy))) Reject("FORBIDDEN", "Source must be authored by Admin.");
        if (topics.Count == 0 && r.TopicIds != null) Reject("CONTENT_INVALID", "No published topics selected.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r, Json))));
        // Both contexts use the same MySQL database; sharing connection and transaction keeps problem copies atomic.
        judge.Database.SetDbConnection(db.Database.GetDbConnection());
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await judge.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM classes WHERE Id = {targetId} FOR UPDATE", ct);
        var receipt = await db.Set<CloneReceipt>().FindAsync([targetId, key], ct);
        if (receipt != null) { if (receipt.RequestHash != hash) Reject("CONFLICT", "Idempotency key reused with different input."); return Ok(JsonSerializer.Deserialize<Guid[]>(receipt.Result)!); }
        var targetChapters = await db.Chapters.Where(c => c.ClassId == targetId).OrderBy(c => c.Order).ToListAsync(ct);
        var insertion = r.AfterChapterId.HasValue ? targetChapters.FindIndex(c => c.Id == r.AfterChapterId) + 1 : targetChapters.Count;
        if (r.AfterChapterId.HasValue && insertion == 0) Reject("CONTENT_INVALID", "Placement chapter not found.");
        var sourceChapters = await db.Chapters.Where(c => c.ClassId == r.SourceClassId).OrderBy(c => c.Order).ToListAsync(ct);
        var chapterMap = new Dictionary<Guid, Guid>();
        foreach (var ch in sourceChapters.Where(ch => r.TopicIds != null ? topics.Any(t => t.ChapterId == ch.Id) : r.ChapterIds == null || r.ChapterIds.Contains(ch.Id)))
        { var copy = new Chapter { ClassId = targetId, Title = ch.Title, SourceChapterId = ch.Id }; db.Chapters.Add(copy); targetChapters.Insert(insertion++, copy); chapterMap[ch.Id] = copy.Id; }
        for (var i = 0; i < targetChapters.Count; i++) targetChapters[i].Order = i;
        var topicIds = topics.Select(t => t.Id).ToArray(); var exercises = await db.TopicExercises.AsNoTracking().Where(e => topicIds.Contains(e.TopicId) && !e.IsDraft).ToListAsync(ct);
        var problemIds = exercises.Select(e => e.ProblemId).Distinct().ToArray();
        var problems = await judge.Problems.AsNoTracking().Include(p => p.TestCases).Include(p => p.StarterCodes).Where(p => problemIds.Contains(p.Id) && p.Status == ProblemStatus.Published).ToListAsync(ct);
        var problemMap = new Dictionary<Guid, Guid>();
        foreach (var p in problems)
        {
            if (!r.CopyProblems) { problemMap[p.Id] = p.Id; continue; }
            var copy = JsonSerializer.Deserialize<Problem>(JsonSerializer.Serialize(p))!; copy.Id = Guid.NewGuid(); copy.ClassId = targetId; copy.CourseId = null; copy.CreatedBy = a.Id; copy.CreatedAt = Now; copy.Status = ProblemStatus.Draft;
            foreach (var test in copy.TestCases) { test.Id = Guid.NewGuid(); test.ProblemId = copy.Id; }
            foreach (var starter in copy.StarterCodes) starter.ProblemId = copy.Id;
            judge.Problems.Add(copy); problemMap[p.Id] = copy.Id;
        }
        var created = new List<Guid>();
        foreach (var t in topics)
        {
            var copy = new Topic { ClassId = targetId, ChapterId = chapterMap[t.ChapterId], Title = t.Title, Slug = t.Slug[..Math.Min(t.Slug.Length, 110)] + "-" + Guid.NewGuid().ToString("N"), Summary = t.Summary, Order = t.Order,
                EstimatedMinutes = t.EstimatedMinutes, CompletionRule = t.CompletionRule, CreatedBy = a.Id, UpdatedAt = Now, SourceTopicId = t.Id, SourcePublishedVersion = t.PublishedVersion,
                DraftContent = JsonSerializer.Serialize(JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, Json)!.Select(b => b with { BlockId = Guid.NewGuid() }), Json) };
            db.Topics.Add(copy); created.Add(copy.Id);
            foreach (var e in exercises.Where(e => e.TopicId == t.Id && problemMap.ContainsKey(e.ProblemId))) db.TopicExercises.Add(new() { TopicId = copy.Id, ProblemId = problemMap[e.ProblemId], Points = e.Points, Order = e.Order, IsRequired = e.IsRequired });
        }
        db.Set<CloneReceipt>().Add(new() { TargetClassId = targetId, Key = key, RequestHash = hash, Result = JsonSerializer.Serialize(created), CreatedAt = Now });
        await judge.SaveChangesAsync(ct); await Save(targetId, a, "Clone", r.SourceClassId, ct); await tx.CommitAsync(ct);
        return Ok(created);
    }
    public async Task<Result<object>> Comments(Guid topicId, Guid? id, string action, CommentInput? input, int page, Actor a, CancellationToken ct)
    {
        var comment = id.HasValue ? await db.TopicComments.SingleOrDefaultAsync(c => c.Id == id, ct) : null;
        if (id.HasValue && comment == null) Reject("NOT_FOUND", "Comment not found.");
        topicId = comment?.TopicId ?? topicId;
        var t = await Topic(topicId, ct); if (t == null) Reject("NOT_FOUND", "Topic not found.");
        await Access(t!.ClassId, a, false, ct); if (!a.IsAdmin && !a.IsTeacher) await CheckUnlocked(t, a, ct);
        if (action == "list") return Ok(await db.TopicComments.AsNoTracking().Where(c => c.TopicId == topicId).OrderByDescending(c => c.IsPinned).ThenBy(c => c.CreatedAt).Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct));
        if (action == "pin" || action == "delete" && comment!.UserId != a.Id) await Access(t.ClassId, a, true, ct);
        if (action == "edit" && comment!.UserId != a.Id) Reject("FORBIDDEN", "Only the author can edit.");
        if (action == "delete") comment!.IsDeleted = true;
        else if (action == "pin") comment!.IsPinned = !comment.IsPinned;
        else
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 2000) Reject("CONTENT_INVALID", "Comment must contain 1–2000 characters.");
            if (comment == null)
            {
                if (input!.ParentId.HasValue && !await db.TopicComments.AnyAsync(c => c.Id == input.ParentId && c.TopicId == topicId && c.ParentId == null, ct)) Reject("CONTENT_INVALID", "Replies support one level only.");
                comment = new() { TopicId = topicId, ClassId = t.ClassId, UserId = a.Id, ParentId = input.ParentId, CreatedAt = Now }; db.TopicComments.Add(comment);
            }
            else comment.EditedAt = Now;
            comment.Body = ContentValidator.Markdown(input!.Body);
        }
        await db.SaveChangesAsync(ct); return Ok(comment!);
    }
    public async Task<Result<object>> Notes(Guid id, string action, NoteInput? input, Actor a, CancellationToken ct)
    {
        var t = await Topic(id, ct); if (t == null) Reject("NOT_FOUND", "Topic not found."); await Access(t!.ClassId, a, false, ct); await CheckUnlocked(t, a, ct);
        var note = await db.TopicNotes.FindAsync([a.Id, id], ct);
        if (action == "get") return Ok(new { note });
        if (action == "delete") { if (note != null) db.TopicNotes.Remove(note); }
        else {
            if (input == null || input.Body == null || input.Body.Length > 10000) Reject("CONTENT_INVALID", "Note too long.");
            if (input!.BlockId.HasValue && !JsonSerializer.Deserialize<ContentBlock[]>(t.PublishedContent, Json)!.Any(b => b.BlockId == input.BlockId)) Reject("BLOCK_INVALID", "Block not found.");
            if (note == null) { note = new() { UserId = a.Id, TopicId = id }; db.TopicNotes.Add(note); }
            note.Body = ContentValidator.Markdown(input.Body); note.BlockId = input.BlockId; note.UpdatedAt = Now;
        }
        await db.SaveChangesAsync(ct); return Ok(new { note });
    }
    public async Task<Result<byte[]>> Export(Guid id, Actor a, CancellationToken ct)
    {
        await Access(id, a, true, ct);
        var rows = await (from p in db.TopicProgress.AsNoTracking() join t in db.Topics on p.TopicId equals t.Id join u in db.Users on p.UserId equals u.Id where p.ClassId == id orderby u.Id, t.Order select new { p.UserId, u.FullName, t.Title, p.Status, p.CompletedAt, p.CompletedLate }).ToListAsync(ct);
        static string Cell(string value) => "\"" + (value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("UserId,FullName,Topic,Status,CompletedAt,CompletedLate\r\n");
        foreach (var r in rows) text.AppendLine(string.Join(",", Cell(r.UserId.ToString()), Cell(r.FullName), Cell(r.Title), Cell(r.Status.ToString()), Cell(r.CompletedAt?.ToString("O") ?? ""), Cell(r.CompletedLate.ToString())));
        return Result<byte[]>.Success(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray());
    }
}
