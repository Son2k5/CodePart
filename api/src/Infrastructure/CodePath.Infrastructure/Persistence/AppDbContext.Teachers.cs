using System.Linq.Expressions;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext
{
    private Expression<Func<User, TeacherDto>> TeacherProjection => u => new(
        u.Id, u.TeacherId, u.FullName, u.Email, u.PhoneNumber, u.Bio, u.FacultyId,
        u.Faculty == null ? null : u.Faculty.Name, u.Status.ToString(),
        ClassTeachers.Count(t => t.TeacherId == u.Id), u.TeachingCourses.Count, u.EmailVerifiedAt, u.CreatedAt, u.LastLoginAt);

    async Task<OffsetPage<TeacherDto>> IAdminDbContext.GetPagedTeachersAsync(TeacherFilter filter, CancellationToken ct)
    {
        var query = Users.AsNoTracking().Where(u => u.Role == UserRole.Teacher);
        if (filter.Status.HasValue) query = query.Where(u => u.Status == filter.Status.Value);
        if (filter.FacultyId.HasValue) query = query.Where(u => u.FacultyId == filter.FacultyId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search)
                || (u.TeacherId != null && u.TeacherId.Contains(search)));
        }
        var count = await query.CountAsync(ct);
        var sorted = filter.SortBy switch
        {
            "name_asc" => query.OrderBy(u => u.FullName),
            "name_desc" => query.OrderByDescending(u => u.FullName),
            "created_asc" => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };
        var items = await sorted.ThenBy(u => u.Id).Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize).Select(TeacherProjection).ToListAsync(ct);
        return new(items, filter.Page, filter.PageSize, count, (int)Math.Ceiling(count / (double)filter.PageSize));
    }

    async Task<TeacherStatsDto> IAdminDbContext.GetTeacherStatsAsync(CancellationToken ct)
    {
        var counts = await Users.AsNoTracking().Where(u => u.Role == UserRole.Teacher)
            .GroupBy(u => u.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(UserStatus s) => counts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        return new(counts.Sum(x => x.Count), Count(UserStatus.Active), Count(UserStatus.Pending),
            Count(UserStatus.Disabled), Count(UserStatus.Rejected));
    }

    async Task<TeacherDetailDto?> IAdminDbContext.GetTeacherDetailByIdAsync(Guid id, CancellationToken ct)
    {
        var profile = await Users.AsNoTracking().Where(u => u.Id == id && u.Role == UserRole.Teacher)
            .Select(TeacherProjection).SingleOrDefaultAsync(ct);
        if (profile is null) return null;
        var classes = await Classes.AsNoTracking().Where(c => c.Teachers.Any(t => t.TeacherId == id)).OrderBy(c => c.Name)
            .Select(c => new ManagedClassDto(c.Id, c.Name, c.AcademicYear, c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active))).ToListAsync(ct);
        var courses = await Courses.AsNoTracking().Where(c => c.TeacherId == id).OrderBy(c => c.SectionCode)
            .Select(c => new TeachingCourseDto(c.Id, c.SubjectName, c.SectionCode, c.Semester, c.MaxCapacity,
                c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active))).ToListAsync(ct);
        return new(profile, classes, courses);
    }

    async Task<IReadOnlyList<FacultyDto>> IAdminDbContext.GetFacultiesAsync(CancellationToken ct) =>
        await Faculties.AsNoTracking().OrderBy(f => f.Name)
            .Select(f => new FacultyDto(f.Id, f.Name, f.Code, f.Users.Count(u => u.Role == UserRole.Teacher)))
            .ToListAsync(ct);
    Task<bool> IAdminDbContext.FacultyExistsAsync(Guid id, CancellationToken ct) => Faculties.AnyAsync(f => f.Id == id, ct);
    Task<bool> IAdminDbContext.TeacherCodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) =>
        Users.AnyAsync(u => u.TeacherId == code && (!exceptId.HasValue || u.Id != exceptId.Value), ct);
}
