using System.Text.Json;
using CodePath.Application.Classes;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Common.Exceptions;
using CodePath.Infrastructure.Persistence;
using CodePath.Infrastructure.Judge;
using CodePath.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;
using MediatR;
using StackExchange.Redis;
namespace CodePath.Infrastructure.Learning;

public sealed partial class LearningService(AppDbContext db, JudgeDbContext judge, TimeProvider clock,
    IConnectionMultiplexer redis, IPublisher publisher, ContentValidator content) : ILearningService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Result<object> Ok(object value) => Result<object>.Success(value);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Reject(string code, string message) => throw new DomainRuleViolationException(message, code);
    private async Task<Class> Access(Guid id, Actor a, bool manage, CancellationToken ct)
    {
        var c = await db.Classes.Include(c => c.Teachers).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (c == null) { Reject("CLASS_NOT_FOUND", "Class not found."); return null!; }
        if (c.IsTemplate) { await TemplateAccess(c, a, manage, ct); return c; }
        if (a.IsAdmin || a.IsTeacher && c.Mode == ClassMode.Managed && c.Teachers.Any(t => t.TeacherId == a.Id)) return c;
        if (manage || a.IsTeacher) Reject("FORBIDDEN", "You cannot manage this class.");
        if (!await db.ClassEnrollments.AnyAsync(e => e.ClassId == id && e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active, ct))
            Reject("NOT_ENROLLED", "Join this class before accessing content.");
        return c;
    }
    private async Task AutoEnroll(Guid id, Actor a, CancellationToken ct)
    {
        if (a.IsAdmin || a.IsTeacher) return;
        if (await db.ClassEnrollments.AnyAsync(e => e.ClassId == id && e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active, ct)) return;
        var repo = (IClassRepository)db;
        await repo.TransactionAsync(async token => {
            var c = await repo.LockAsync(id, null, token);
            if (c == null || c.AccessType != ClassAccessType.Public) Reject("NOT_ENROLLED", "Enrollment is required.");
            var e = await repo.EnrollmentAsync(id, a.Id, token);
            if (e?.Status == ClassEnrollmentStatus.Active) return true;
            c!.EnsureCanJoin(false, await repo.CountStudentsAsync(id, token), false, Now);
            if (e == null) await repo.AddEnrollmentAsync(ClassEnrollment.Create(id, a.Id, ClassJoinMethod.Public, Now), token);
            else e.Rejoin(ClassJoinMethod.Public, Now);
            await repo.SaveAsync(token); return true;
        }, ct);
    }
    private Task<Topic?> Topic(Guid id, CancellationToken ct) => db.Topics.SingleOrDefaultAsync(t => t.Id == id && db.Chapters.Any(c => c.Id == t.ChapterId), ct);
    private async Task<List<CodePath.Domain.Learning.Topic>> Ordered(Guid id, CancellationToken ct) => await
        (from t in db.Topics join c in db.Chapters on t.ChapterId equals c.Id where t.ClassId == id orderby c.Order, c.Id, t.Order, t.Id select t).ToListAsync(ct);
    private async Task CheckUnlocked(Topic t, Actor a, CancellationToken ct)
    {
        var ordered = await Ordered(t.ClassId, ct); var i = ordered.FindIndex(x => x.Id == t.Id);
        var previous = i <= 0 ? null : ordered[i - 1];
        var done = previous == null || await db.TopicProgress.AnyAsync(p => p.UserId == a.Id && p.TopicId == previous.Id && p.Status == LearningStatus.Completed, ct);
        var reason = t.LockedReason(Now, done);
        if (reason != null) Reject("TOPIC_LOCKED", reason);
    }
    private async Task Save(Guid classId, Actor actor, string action, Guid? subject, CancellationToken ct)
    {
        await ((IClassRepository)db).AuditAsync(actor.Id, classId, "Learning." + action, subject, Now, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { Reject("CONFLICT", "Content changed. Reload before saving."); }
    }
    public async Task<Result<object>> Mode(Guid id, ClassMode mode, Actor a, CancellationToken ct)
    {
        if (!a.IsAdmin) Reject("FORBIDDEN", "Admin only.");
        return await ((IClassRepository)db).TransactionAsync(async token => {
            var c = await ((IClassRepository)db).LockAsync(id, null, token);
            if (c == null) Reject("CLASS_NOT_FOUND", "Class not found.");
            if (mode == ClassMode.SelfStudy && (await db.Topics.AnyAsync(t => t.ClassId == id && t.DueAt != null, token)
                || await db.ClassScoreConfigs.AnyAsync(s => s.ClassId == id, token) || await db.ScoreEntries.AnyAsync(s => s.ClassId == id, token)))
                Reject("CLASS_MODE_NOT_SUPPORTED", "Remove deadlines and score configuration before changing mode.");
            c!.SetMode(mode, Now); await Save(id, a, "Mode", id, token); return Ok(new { c.Id, c.Mode });
        }, ct);
    }
    public async Task<Result<object>> Chapters(Guid classId, Guid? id, string action, ChapterInput? input, ReorderInput? reorder, Actor a, CancellationToken ct)
    {
        await Access(classId, a, action != "list", ct);
        var chapters = await db.Chapters.Where(x => x.ClassId == classId).OrderBy(x => x.Order).ToListAsync(ct);
        if (action == "list") return Ok(chapters);
        if (action == "reorder")
        {
            if (reorder == null || !chapters.Select(x => x.Id).Order().SequenceEqual(reorder.Ids.Distinct().Order()) || reorder.Ids.Length != chapters.Count) Reject("CONTENT_INVALID", "Provide each chapter exactly once.");
            for (var i = 0; i < reorder!.Ids.Length; i++) chapters.Single(x => x.Id == reorder.Ids[i]).Order = i;
        }
        else
        {
            var chapter = id.HasValue ? chapters.SingleOrDefault(x => x.Id == id) : new Chapter { ClassId = classId };
            if (chapter == null) Reject("NOT_FOUND", "Chapter not found.");
            if (action == "delete")
            {
                if (await db.Topics.AnyAsync(t => t.ChapterId == id, ct)) Reject("CONFLICT", "Move or delete topics first.");
                chapter!.IsDeleted = true;
            }
            else {
                if (input == null || string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 250 || input.Order < 0) Reject("CONTENT_INVALID", "Invalid chapter.");
                chapter!.Title = ContentValidator.Plain(input!.Title); chapter.Order = input.Order;
                if (!id.HasValue) db.Chapters.Add(chapter);
            }
            id = chapter!.Id;
        }
        await Save(classId, a, "Chapter." + action, id, ct); return Ok(new { id });
    }
    public async Task<Result<object>> Topics(Guid classId, Guid? id, string action, TopicInput? input, ReorderInput? reorder, Actor a, CancellationToken ct)
    {
        var ownerClass = await Access(classId, a, true, ct);
        if (action == "list") return Ok(await Ordered(classId, ct));
        if (action == "reorder")
        {
            if (reorder == null || reorder.ChapterId == null || reorder.Ids.Length != reorder.Ids.Distinct().Count()
                || !await db.Chapters.AnyAsync(c => c.Id == reorder.ChapterId && c.ClassId == classId, ct)) Reject("CONTENT_INVALID", "Invalid destination.");
            var topics = await db.Topics.Where(t => t.ClassId == classId && (t.ChapterId == reorder!.ChapterId || reorder.Ids.Contains(t.Id))).ToListAsync(ct);
            if (topics.Count != reorder!.Ids.Length) Reject("CONTENT_INVALID", "Include all destination topics and only topics in this class.");
            for (var i = 0; i < reorder.Ids.Length; i++) { var t = topics.Single(x => x.Id == reorder.Ids[i]); t.Order = i; t.ChapterId = reorder.ChapterId!.Value; t.RowVersion = Guid.NewGuid(); }
            await Save(classId, a, "Reorder", null, ct); return Ok(new { updated = topics.Count });
        }
        var topic = id.HasValue ? await Topic(id.Value, ct) : new Topic { ClassId = classId, CreatedBy = a.Id, UpdatedAt = Now };
        if (topic == null || topic.ClassId != classId) Reject("NOT_FOUND", "Topic not found.");
        if (action == "get") return Ok(topic!);
        var hasProgress = false;
        if (action == "delete") { topic!.IsDeleted = true; hasProgress = await db.TopicProgress.AnyAsync(p => p.TopicId == id, ct); }
        else if (action == "duplicate")
        {
            var copy = new Topic { ClassId = classId, ChapterId = topic!.ChapterId, Title = topic.Title, Slug = topic.Slug[..Math.Min(110, topic.Slug.Length)] + "-" + Guid.NewGuid().ToString("N"), Summary = topic.Summary, Order = topic.Order + 1,
                EstimatedMinutes = topic.EstimatedMinutes, CreatedBy = a.Id, UpdatedAt = Now, CompletionRule = topic.CompletionRule,
                DraftContent = JsonSerializer.Serialize(JsonSerializer.Deserialize<ContentBlock[]>(topic.DraftContent, Json)!.Select(b => b with { BlockId = Guid.NewGuid() }), Json) };
            db.Topics.Add(copy);
            var exercises = await db.TopicExercises.AsNoTracking().Where(e => e.TopicId == topic.Id).ToListAsync(ct);
            db.TopicExercises.AddRange(exercises.Select(e => new TopicExercise { TopicId = copy.Id, ProblemId = e.ProblemId, Points = e.Points, Order = e.Order, IsRequired = e.IsRequired, IsDraft = e.IsDraft }));
            topic = copy;
        }
        else {
            if (input == null || !new TopicInputValidator().Validate(input).IsValid || !await db.Chapters.AnyAsync(c => c.Id == input.ChapterId && c.ClassId == classId, ct)) Reject("CONTENT_INVALID", "Invalid topic fields.");
            if (await db.Topics.IgnoreQueryFilters().AnyAsync(t => t.ClassId == classId && t.Slug == input!.Slug && t.Id != topic!.Id, ct)) Reject("CONFLICT", "Slug already exists.");
            if (ownerClass.IsTemplate && input!.UnlockAt.HasValue) Reject("CONTENT_INVALID", "Templates cannot have fixed unlock dates.");
            topic!.ChapterId = input!.ChapterId; topic.Title = ContentValidator.Plain(input.Title); topic.Slug = input.Slug; topic.Summary = ContentValidator.Plain(input.Summary);
            topic.Order = input.Order; topic.EstimatedMinutes = input.EstimatedMinutes; topic.UnlockRule = input.UnlockRule; topic.UnlockAt = input.UnlockAt;
            if (!id.HasValue) db.Topics.Add(topic);
        }
        topic!.UpdatedAt = Now; topic.RowVersion = Guid.NewGuid(); await Save(classId, a, "Topic." + action, topic.Id, ct);
        return Ok(new { topic.Id, topic.RowVersion, warning = hasProgress ? "TOPIC_HAS_PROGRESS" : null });
    }
    public async Task<Result<object>> Edit(Guid id, string action, object? input, Actor a, CancellationToken ct)
    {
        var t = await Topic(id, ct); if (t == null) Reject("NOT_FOUND", "Topic not found.");
        var c = await Access(t!.ClassId, a, true, ct);
        switch (action)
        {
            case "draft":
                var draft = (DraftInput)input!;
                if (draft.RowVersion != t.RowVersion) Reject("CONFLICT", "Draft changed. Reload before saving.");
                t.DraftContent = await content.Validate(draft.Blocks, a, t, ct); break;
            case "publish": case "schedule":
                await content.Validate(JsonSerializer.Deserialize<ContentBlock[]>(t.DraftContent, Json)!, a, t, ct);
                t.PublishAt = action == "schedule" ? (DateTime?)input : null;
                if (action == "schedule" && (t.PublishAt <= Now || t.PublishAt?.Kind != DateTimeKind.Utc)) Reject("CONTENT_INVALID", "Schedule must be a future UTC timestamp.");
                t.Publish(Now);
                var index = await db.TopicSearch.FindAsync([id], ct);
                if (index == null) { index = new() { TopicId = id, ClassId = t.ClassId }; db.TopicSearch.Add(index); }
                index.Document = ContentValidator.SearchText(t); index.UpdatedAt = Now; break;
            case "unpublish": t.Status = TopicStatus.Draft; break;
            case "completion-rule":
                var rule = (CompletionRule)input!; if (!new CompletionRuleValidator().Validate(rule).IsValid) Reject("CONTENT_INVALID", "Invalid completion rule.");
                t.CompletionRule = JsonSerializer.Serialize(rule, Json); break;
            case "due-date":
                if (c.IsTemplate || c.Mode != ClassMode.Managed) Reject("DEADLINE_NOT_ALLOWED", "SelfStudy has no deadlines.");
                var date = (DateTime?)input; if (date.HasValue && date.Value.Kind != DateTimeKind.Utc) Reject("CONTENT_INVALID", "UTC timestamp required.");
                t.DueAt = date; break;
            case "exercises":
                var exercises = (ExerciseInput[])input!;
                await ValidateExercises(t.ClassId, exercises, a, ct, t.Id);
                var ids = exercises.Select(e => e.ProblemId).ToArray();
                var old = await db.TopicExercises.Where(e => e.TopicId == id).ToListAsync(ct);
                db.TopicExercises.RemoveRange(old.Where(e => !ids.Contains(e.ProblemId)));
                foreach (var e in exercises) { var row = old.SingleOrDefault(x => x.ProblemId == e.ProblemId); if (row == null) { row = new() { TopicId = id, ProblemId = e.ProblemId }; db.TopicExercises.Add(row); } row.Order = e.Order; row.Points = e.Points; row.IsRequired = e.IsRequired; }
                break;
            default: Reject("CONTENT_INVALID", "Unknown action."); break;
        }
        t.UpdatedAt = Now; t.RowVersion = Guid.NewGuid(); await Save(t.ClassId, a, action, id, ct);
        return Ok(new { t.Id, t.RowVersion, t.PublishedVersion });
    }
}
