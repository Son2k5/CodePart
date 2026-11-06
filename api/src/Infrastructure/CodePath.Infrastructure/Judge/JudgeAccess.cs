using CodePath.Application.Judge;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Judge;

public sealed class JudgeAccess(AppDbContext db) : IJudgeAccess
{
    public async Task<IReadOnlyList<JudgeScope>> Scopes(Actor a, bool manage, CancellationToken ct)
    {
        var classes = await db.Classes.AsNoTracking().Where(c => !c.IsDeleted && (manage
            ? a.IsAdmin || a.IsTeacher && c.Teachers.Any(t => t.TeacherId == a.Id)
            : c.Status == ClassStatus.Active && c.ClassEnrollments.Any(e => e.StudentId == a.Id && e.Status == ClassEnrollmentStatus.Active)))
            .OrderBy(c => c.Name).Select(c => new JudgeScope(c.Id, c.Name, "class")).ToListAsync(ct);
        return classes;
    }
    public async Task<bool> CanAccess(Guid? classId, Actor a, bool manage, CancellationToken ct)
    {
        if (!classId.HasValue) return !manage || a.IsAdmin || a.IsTeacher;
        var scopes = await Scopes(a, manage, ct);
        return scopes.Any(s => s.Id == classId.Value);
    }
}
