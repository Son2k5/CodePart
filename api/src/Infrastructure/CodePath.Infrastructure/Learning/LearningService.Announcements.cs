using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Application.Notifications.Events;
using CodePath.Domain.Learning;
using CodePath.Shared.Kernel.Common;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Learning;

public sealed partial class LearningService : IClassAnnouncementService
{
    public async Task<Result<object>> List(Guid classId, Actor a, CancellationToken ct)
    {
        await Access(classId, a, false, ct);
        var rows = await (from x in db.ClassAnnouncements.AsNoTracking()
                          where x.ClassId == classId
                          join u in db.Users.AsNoTracking() on x.SenderId equals u.Id
                          orderby x.CreatedAt descending, x.Id
                          select new ClassAnnouncementDto(x.Id, x.Title, x.Body, u.FullName, x.CreatedAt)).ToListAsync(ct);
        return Ok(rows);
    }

    public async Task<Result<object>> Create(Guid classId, ClassAnnouncementInput input, Actor a, CancellationToken ct)
    {
        await Access(classId, a, true, ct);
        if (!new ClassAnnouncementInputValidator().Validate(input).IsValid) Reject("CONTENT_INVALID", "Announcement requires a title and body.");
        var announcement = ClassAnnouncement.Create(classId, a.Id, ContentValidator.Plain(input.Title), ContentValidator.Plain(input.Body), Now);
        db.ClassAnnouncements.Add(announcement);
        // Fan out an in-app notification to every enrolled student; the outbox row commits with SaveChanges below.
        await events.PublishAsync(new ClassAnnouncementPublishedNotification(classId, announcement.Id, announcement.Title), ct);
        await Save(classId, a, "Announcement.Create", announcement.Id, ct);
        return Ok(new { announcement.Id, announcement.CreatedAt });
    }

    public async Task<Result<object>> Delete(Guid classId, Guid id, Actor a, CancellationToken ct)
    {
        await Access(classId, a, true, ct);
        var announcement = await db.ClassAnnouncements.SingleOrDefaultAsync(x => x.Id == id && x.ClassId == classId, ct);
        if (announcement == null) Reject("NOT_FOUND", "Announcement not found.");
        announcement!.Delete(Now);
        await Save(classId, a, "Announcement.Delete", id, ct);
        return Ok(new { id, deleted = true });
    }
}
