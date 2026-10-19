using System.Text.Json;
using CodePath.Application.Classes;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Learning;

public sealed partial class LearningService : ICurriculumService
{
    public async Task<bool> CanReadLinkedProblem(Guid problemId, Actor a, CancellationToken ct)
    {
        var candidates = await db.Topics.Where(t => t.Status == TopicStatus.Published &&
            (t.PublishAt == null || t.PublishAt <= Now) && db.Chapters.Any(ch => ch.Id == t.ChapterId) &&
            db.Classes.Any(c => c.Id == t.ClassId && !c.IsTemplate && c.Status == ClassStatus.Active &&
                c.ClassEnrollments.Any(e => e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active)) &&
            db.TopicExercises.Any(e => e.TopicId == t.Id && e.ProblemId == problemId && !e.IsDraft)).ToListAsync(ct);
        foreach (var t in candidates)
        {
            try { await CheckUnlocked(t, a, ct); return true; }
            catch (CodePath.Domain.Common.Exceptions.DomainRuleViolationException) { }
        }
        return false;
    }

    private async Task<IQueryable<Class>> VisibleTemplates(Actor a, CancellationToken ct)
    {
        var adminIds = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
        var owners = adminIds.Select(id => id.ToString()).Append(a.Id.ToString()).ToArray();
        return db.Classes.Where(c => c.IsTemplate && (a.IsAdmin || owners.Contains(c.CreatedBy!)));
    }

    private async Task TemplateAccess(Class c, Actor a, bool manage, CancellationToken ct)
    {
        if (!a.IsAdmin && !(a.IsTeacher && (c.CreatedBy == a.Id.ToString() ||
            !manage && Guid.TryParse(c.CreatedBy, out var owner) && await db.Users.AnyAsync(u => u.Id == owner && u.Role == UserRole.Admin, ct))))
            Reject("FORBIDDEN", "Template access denied.");
    }

    private static object TemplateDto(Class c) => new { c.Id, c.Name, c.Description, c.Thumbnail, c.FacultyId, c.IsTemplate, c.CreatedBy, c.CreatedAt, c.RowVersion };

    public async Task<Result<object>> Templates(Guid? id, string action, CurriculumTemplateInput? input, bool confirm, Actor a, CancellationToken ct)
    {
        if (!a.IsAdmin && !a.IsTeacher) Reject("FORBIDDEN", "Teacher or Admin required.");
        if (action == "list") return Ok((await (await VisibleTemplates(a, ct)).AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(ct)).Select(TemplateDto));
        Class? c = null;
        if (id.HasValue)
        {
            c = await db.Classes.SingleOrDefaultAsync(c => c.Id == id && c.IsTemplate, ct);
            if (c == null) Reject("NOT_FOUND", "Template not found.");
            await TemplateAccess(c!, a, action != "get", ct);
        }
        if (action == "get") return Ok(new { template = TemplateDto(c!), chapters = await db.Chapters.Where(ch => ch.ClassId == id).OrderBy(ch => ch.Order).ToListAsync(ct), topics = await Ordered(id!.Value, ct) });
        if (action == "delete")
        {
            if (!confirm && (await db.Topics.AnyAsync(t => t.SourceTopicId != null && db.Topics.IgnoreQueryFilters().Any(s => s.Id == t.SourceTopicId && s.ClassId == id), ct) ||
                await db.Chapters.AnyAsync(ch => ch.SourceChapterId != null && db.Chapters.IgnoreQueryFilters().Any(s => s.Id == ch.SourceChapterId && s.ClassId == id), ct)))
                Reject("CONFLICT", "Template has derived topics; confirm=true is required.");
            c!.Delete(0, false, false, Now);
        }
        else
        {
            if (input == null) Reject("CONTENT_INVALID", "Template metadata required.");
            if (input!.FacultyId.HasValue && !await db.Faculties.AnyAsync(f => f.Id == input.FacultyId, ct)) Reject("CONTENT_INVALID", "Faculty not found.");
            if (c == null) { c = Class.CreateTemplate(input.Name, input.Description, a.Id, Now); db.Classes.Add(c); }
            c.ConfigureTemplate(input.Name, input.Description, input.Thumbnail, input.FacultyId, Now);
        }
        await Save(c!.Id, a, "Template." + action, c.Id, ct);
        return Ok(TemplateDto(c));
    }

    private IQueryable<Problem> UsableProblems(Guid classId, Actor a) => judge.Problems.Where(p =>
        a.IsAdmin || p.ClassId == classId || p.CreatedBy == a.Id || p.ClassId == null && p.CourseId == null && p.Status == ProblemStatus.Published);

    private async Task ValidateExercises(Guid classId, ExerciseInput[] exercises, Actor a, CancellationToken ct, Guid? topicId = null)
    {
        if (exercises == null || exercises.Length > 200 || exercises.Any(e => e == null || e.ProblemId == Guid.Empty || e.Points is < 0 or > 10000 || e.Order < 0) ||
            exercises.Select(e => e.ProblemId).Distinct().Count() != exercises.Length)
            Reject("CONTENT_INVALID", "Invalid exercises.");
        var ids = exercises!.Select(e => e.ProblemId).ToArray();
        var allowed = await UsableProblems(classId, a).Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        // A copied topic retains permission to keep its existing problem links.
        if (topicId.HasValue)
        {
            var linked = await db.TopicExercises.Where(e => e.TopicId == topicId && ids.Contains(e.ProblemId)).Select(e => e.ProblemId).ToListAsync(ct);
            allowed.AddRange(await judge.Problems.Where(p => linked.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct));
        }
        if (allowed.Distinct().Count() != ids.Length)
            Reject("FORBIDDEN", "Problem is not available to this class.");
    }

    public async Task<Result<object>> AvailableProblems(Guid classId, string? search, string? difficulty, int page, int pageSize, Actor a, CancellationToken ct)
    {
        await Access(classId, a, true, ct);
        if (search?.Length > 200 || difficulty != null && difficulty is not ("Easy" or "Medium" or "Hard")) Reject("CONTENT_INVALID", "Invalid problem filter.");
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = UsableProblems(classId, a).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Title.Contains(search.Trim()));
        if (difficulty != null) query = query.Where(p => p.Difficulty == difficulty);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Title).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.Id, p.Title, p.Difficulty, p.Points, p.ClassId, p.CreatedBy, status = p.Status.ToString() }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }

    public Task<Result<object>> CreateFullTopic(Guid classId, CreateFullTopicInput r, Actor a, CancellationToken ct) =>
        ((IClassRepository)db).TransactionAsync(async token =>
        {
            var c = await Access(classId, a, true, token);
            var metadata = new TopicInput(r.ChapterId, r.Title, r.Slug, r.Summary, r.Order, r.EstimatedMinutes, r.UnlockRule, r.UnlockAt);
            if (!new TopicInputValidator().Validate(metadata).IsValid || r.LectureContent == null || r.CompletionRule == null ||
                !new CompletionRuleValidator().Validate(r.CompletionRule).IsValid || r.DueAt.HasValue && r.DueAt.Value.Kind != DateTimeKind.Utc ||
                !await db.Chapters.AnyAsync(ch => ch.Id == r.ChapterId && ch.ClassId == classId, token)) Reject("CONTENT_INVALID", "Invalid topic fields.");
            if (r.DueAt.HasValue && (c.IsTemplate || c.Mode != ClassMode.Managed)) Reject("DEADLINE_NOT_ALLOWED", "Templates and SelfStudy have no deadlines.");
            if (c.IsTemplate && r.UnlockAt.HasValue) Reject("CONTENT_INVALID", "Templates cannot have fixed unlock dates.");
            if (await db.Topics.IgnoreQueryFilters().AnyAsync(t => t.ClassId == classId && t.Slug == r.Slug, token)) Reject("CONFLICT", "Slug already exists.");
            await ValidateExercises(classId, r.Exercises, a, token);
            var t = new Topic { ClassId = classId, ChapterId = r.ChapterId, Title = ContentValidator.Plain(r.Title), Slug = r.Slug,
                Summary = ContentValidator.Plain(r.Summary), Order = r.Order, EstimatedMinutes = r.EstimatedMinutes, UnlockRule = r.UnlockRule,
                UnlockAt = r.UnlockAt, DueAt = r.DueAt, CreatedBy = a.Id, UpdatedAt = Now, CompletionRule = JsonSerializer.Serialize(r.CompletionRule, Json) };
            var blocks = new List<ContentBlock>();
            void Block(string type, object data) => blocks.Add(new(Guid.NewGuid(), type, JsonSerializer.SerializeToElement(data, Json)));
            var lecture = r.LectureContent!;
            if (lecture.Markdown != null) Block("Markdown", new { markdown = lecture.Markdown });
            if (lecture.Video != null) Block("Video", lecture.Video);
            foreach (var document in lecture.Documents ?? [])
            {
                if (document == null) Reject("CONTENT_INVALID", "Invalid document.");
                Block("Pdf", new { assetId = document!.AssetId, title = document.Title });
            }
            t.DraftContent = await content.Validate(blocks.ToArray(), a, t, token);
            db.Topics.Add(t);
            db.TopicExercises.AddRange(r.Exercises.Select(e => new TopicExercise { TopicId = t.Id, ProblemId = e.ProblemId, Order = e.Order, Points = e.Points, IsRequired = e.IsRequired }));
            if (r.PublishImmediately)
            {
                t.Publish(Now);
                db.TopicSearch.Add(new() { TopicId = t.Id, ClassId = classId, Document = ContentValidator.SearchText(t), UpdatedAt = Now });
            }
            await Save(classId, a, "Topic.CreateFull", t.Id, token);
            return Ok(new { t.Id, t.RowVersion, t.Status, t.PublishedVersion });
        }, ct);

    public async Task<Result<object>> ApplyTemplate(Guid classId, ApplyTemplateInput input, string key, Actor a, CancellationToken ct)
    {
        var target = await Access(classId, a, true, ct);
        if (target.IsTemplate) Reject("CONTENT_INVALID", "Destination must be a class.");
        var source = await db.Classes.SingleOrDefaultAsync(c => c.Id == input.TemplateId && c.IsTemplate, ct);
        if (source == null) Reject("NOT_FOUND", "Template not found.");
        await TemplateAccess(source!, a, false, ct);
        return await Clone(classId, new(input.TemplateId, input.ChapterIds, null, input.CopyProblems, input.AfterChapterId), key, a, ct);
    }
}
