using System.Linq.Expressions;
using System.Text.Json;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext
{
    private Expression<Func<User, StudentDto>> StudentProjection => u => new(
        u.Id, u.StudentId, u.FullName, u.Email, u.PhoneNumber, u.Bio, u.FacultyId,
        u.Faculty == null ? (u.Class == null ? null : u.Class.Faculty.Name) : u.Faculty.Name,
        u.Status.ToString(),
        ClassEnrollments.Count(e => e.StudentId == u.Id && e.Status == ClassEnrollmentStatus.Active) + (u.ClassId != null ? 1 : 0),
        Enrollments.Count(e => e.StudentId == u.Id && e.Status == EnrollmentStatus.Active),
        u.EmailVerifiedAt, u.CreatedAt, u.LastLoginAt);

    async Task<OffsetPage<StudentDto>> IAdminDbContext.GetPagedStudentsAsync(StudentFilter filter, CancellationToken ct)
    {
        var query = Users.AsNoTracking().Where(u => u.Role == UserRole.Student);
        if (filter.Status.HasValue) query = query.Where(u => u.Status == filter.Status.Value);
        if (filter.FacultyId.HasValue)
        {
            var fId = filter.FacultyId.Value;
            query = query.Where(u => u.FacultyId == fId || (u.Class != null && u.Class.FacultyId == fId));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search)
                || (u.StudentId != null && u.StudentId.Contains(search)));
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
            .Take(filter.PageSize).Select(StudentProjection).ToListAsync(ct);
        return new(items, filter.Page, filter.PageSize, count, (int)Math.Ceiling(count / (double)filter.PageSize));
    }

    async Task<StudentStatsDto> IAdminDbContext.GetStudentStatsAsync(CancellationToken ct)
    {
        var counts = await Users.AsNoTracking().Where(u => u.Role == UserRole.Student)
            .GroupBy(u => u.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(UserStatus s) => counts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        return new(counts.Sum(x => x.Count), Count(UserStatus.Active), Count(UserStatus.Pending),
            Count(UserStatus.Disabled), Count(UserStatus.Rejected));
    }

    async Task<StudentDetailDto?> IAdminDbContext.GetStudentDetailByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await Users.AsNoTracking().Where(u => u.Id == id && u.Role == UserRole.Student)
            .SingleOrDefaultAsync(ct);
        if (user is null) return null;

        var profile = await Users.AsNoTracking().Where(u => u.Id == id)
            .Select(StudentProjection).SingleAsync(ct);

        var enrolledClasses = await ClassEnrollments.AsNoTracking()
            .Where(e => e.StudentId == id && e.Status == ClassEnrollmentStatus.Active)
            .Select(e => new EnrolledClassDto(e.Class.Id, e.Class.Name, e.Class.AcademicYear))
            .ToListAsync(ct);

        if (user.ClassId.HasValue && !enrolledClasses.Any(c => c.Id == user.ClassId.Value))
        {
            var primaryClass = await Classes.AsNoTracking().Where(c => c.Id == user.ClassId.Value)
                .Select(c => new EnrolledClassDto(c.Id, c.Name, c.AcademicYear))
                .SingleOrDefaultAsync(ct);
            if (primaryClass is not null) enrolledClasses.Add(primaryClass);
        }

        var enrolledCourses = await Enrollments.AsNoTracking()
            .Where(e => e.StudentId == id && e.Status == EnrollmentStatus.Active)
            .Select(e => new EnrolledCourseDto(e.Course.Id, e.Course.SubjectName, e.Course.SectionCode, e.Course.Semester,
                e.Course.Teacher != null ? e.Course.Teacher.FullName : null))
            .ToListAsync(ct);

        return new(profile, enrolledClasses, enrolledCourses);
    }

    Task<bool> IAdminDbContext.StudentCodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) =>
        Users.AnyAsync(u => u.StudentId == code && (!exceptId.HasValue || u.Id != exceptId.Value), ct);

    async Task<Result<bool>> IAdminDbContext.DeleteUserAsync(Guid actorId, Guid targetUserId, string? ipAddress, CancellationToken ct)
    {
        if (actorId == targetUserId)
            return Result<bool>.Failure("Bạn không thể xóa tài khoản của chính mình.", ErrorCodes.Conflict);

        var target = await Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
        if (target is null)
            return Result<bool>.Failure("Tài khoản người dùng không tồn tại.", ErrorCodes.NotFound);

        if (target.Role == UserRole.Teacher)
        {
            var hasCourses = await Courses.AnyAsync(c => c.TeacherId == targetUserId, ct);
            if (hasCourses)
                return Result<bool>.Failure("Không thể xóa giáo viên đang phụ trách học phần. Vui lòng chuyển giao học phần trước khi xóa.", ErrorCodes.Conflict);

            var hasClasses = await Classes.AnyAsync(c => c.TeacherId == targetUserId, ct);
            if (hasClasses)
                return Result<bool>.Failure("Không thể xóa giáo viên đang chủ nhiệm lớp học. Vui lòng bàn giao lớp trước khi xóa.", ErrorCodes.Conflict);

            await ClassTeachers.Where(t => t.TeacherId == targetUserId || t.AssignedBy == targetUserId).ExecuteDeleteAsync(ct);
        }
        else if (target.Role == UserRole.Student)
        {
            await ClassEnrollments.Where(e => e.StudentId == targetUserId).ExecuteDeleteAsync(ct);
            await Enrollments.Where(e => e.StudentId == targetUserId).ExecuteDeleteAsync(ct);
            await DeadlineExtensions.Where(d => d.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await ScoreEntries.Where(s => s.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await TopicProgress.Where(p => p.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await BlockProgress.Where(p => p.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await ActivityDays.Where(a => a.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await UserBadges.Where(b => b.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await TopicComments.Where(c => c.UserId == targetUserId).ExecuteDeleteAsync(ct);
            await TopicNotes.Where(n => n.UserId == targetUserId).ExecuteDeleteAsync(ct);

            var subIds = await Submissions.Where(s => s.StudentId == targetUserId).Select(s => s.Id).ToListAsync(ct);
            if (subIds.Count > 0)
            {
                await SubmissionTestResults.Where(r => subIds.Contains(r.SubmissionId)).ExecuteDeleteAsync(ct);
                await Submissions.Where(s => s.StudentId == targetUserId).ExecuteDeleteAsync(ct);
            }
        }

        await RefreshTokens.Where(r => r.UserId == targetUserId).ExecuteDeleteAsync(ct);
        await PasswordResetTokens.Where(p => p.UserId == targetUserId).ExecuteDeleteAsync(ct);

        // Remove audit logs referencing this user to prevent FK restrict violation
        await AuditLogs.Where(a => a.TargetUserId == targetUserId || a.ActorId == targetUserId).ExecuteDeleteAsync(ct);

        var auditData = JsonSerializer.Serialize(new
        {
            deletedUserId = target.Id,
            email = target.Email,
            fullName = target.FullName,
            role = target.Role.ToString(),
            studentId = target.StudentId,
            teacherId = target.TeacherId
        });

        Users.Remove(target);
        await SaveChangesAsync(ct);

        var utcNow = DateTime.UtcNow;
        var auditLog = AuditLog.Create(actorId, AuditAction.UserDeleted, actorId, ipAddress, auditData, utcNow);
        AuditLogs.Add(auditLog);
        await SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
