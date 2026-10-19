using CodePath.Application.Judge;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
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
        var courses = await db.Courses.AsNoTracking().Where(c => manage
            ? a.IsAdmin || a.IsTeacher && c.TeacherId == a.Id
            : c.Status == CourseStatus.Active && c.Enrollments.Any(e => e.StudentId == a.Id && e.Status == EnrollmentStatus.Active))
            .OrderBy(c => c.SubjectName).Select(c => new JudgeScope(c.Id, c.SubjectName + " · " + c.SectionCode, "course")).ToListAsync(ct);
        return classes.Concat(courses).ToArray();
    }
    public async Task<bool> CanAccess(Guid? courseId, Guid? classId, Actor a, bool manage, CancellationToken ct)
    {
        if (courseId.HasValue && classId.HasValue) return false;
        if (!courseId.HasValue && !classId.HasValue) return !manage || a.IsAdmin || a.IsTeacher; // Legacy public problems.
        var scopes = await Scopes(a, manage, ct);
        return scopes.Any(s => s.Id == (classId ?? courseId) && s.Kind == (classId.HasValue ? "class" : "course"));
    }
}
