using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
namespace CodePath.Infrastructure.Learning;

public sealed partial class LearningService : ILearningRagAccess
{
    public async Task<object> RagClasses(Actor actor, CancellationToken ct) => await db.Classes.AsNoTracking()
        .Where(c => !c.IsTemplate && c.Status == ClassStatus.Active &&
            db.ClassEnrollments.Any(e => e.ClassId == c.Id && e.StudentId == actor.Id && e.Status == ClassEnrollmentStatus.Active))
        .OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
    public async Task<RagTopic> RagTopic(Guid id, Actor actor, bool manage, CancellationToken ct)
    {
        // No tracked Topic may satisfy a later authorization check after publication/revocation.
        var t = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && db.Chapters.Any(c => c.Id == t.ChapterId), ct);
        if (t == null) Reject("NOT_FOUND", "Topic not found.");
        var owner = await db.Classes.AsNoTracking().Include(c => c.Teachers).SingleOrDefaultAsync(c => c.Id == t!.ClassId, ct);
        if (owner == null) Reject("NOT_FOUND", "Class not found.");
        if (owner!.IsTemplate) await TemplateAccess(owner, actor, manage, ct);
        else if (!(actor.IsAdmin || actor.IsTeacher && owner.Mode == ClassMode.Managed && owner.Teachers.Any(x => x.TeacherId == actor.Id)))
        {
            if (manage || actor.IsTeacher) Reject("FORBIDDEN", "Class management access required.");
            if (!await db.ClassEnrollments.AnyAsync(e => e.ClassId == owner.Id && e.StudentId == actor.Id && e.Status == ClassEnrollmentStatus.Active, ct))
                Reject("NOT_ENROLLED", "Enrollment required.");
        }
        if (!actor.IsAdmin && !actor.IsTeacher && (owner.IsTemplate || owner.Status != ClassStatus.Active))
            Reject("FORBIDDEN", "Class is not active.");
        if (!manage)
        {
            var ordered = await (from item in db.Topics.AsNoTracking() join chapter in db.Chapters on item.ChapterId equals chapter.Id
                where item.ClassId == t!.ClassId orderby chapter.Order, chapter.Id, item.Order, item.Id select item.Id).ToListAsync(ct);
            var position = ordered.IndexOf(t!.Id);
            var previousDone = position <= 0 || await db.TopicProgress.AnyAsync(p => p.UserId == actor.Id &&
                p.TopicId == ordered[position - 1] && p.Status == LearningStatus.Completed, ct);
            if (t.LockedReason(Now, previousDone) is { } reason) Reject("TOPIC_LOCKED", reason);
        }
        return new(t.Id, t.ClassId, t.RowVersion, t.PublishedVersion, t.Title, t.Slug, t.PublishedContent);
    }

    public async Task<Guid[]> RagProblemTopics(Guid problemId, Actor actor, CancellationToken ct)
    {
        return await db.Topics.AsNoTracking().Where(t => t.Status == TopicStatus.Published &&
            db.TopicExercises.Any(e => e.TopicId == t.Id && e.ProblemId == problemId && !e.IsDraft) &&
            db.ClassEnrollments.Any(e => e.ClassId == t.ClassId && e.StudentId == actor.Id && e.Status == ClassEnrollmentStatus.Active))
            .OrderBy(t => t.Id).Select(t => t.Id).Take(20).ToArrayAsync(ct);
    }
}
