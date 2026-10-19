using System.Data;
using System.Text.Json;
using CodePath.Application.Classes;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext : IClassRepository
{
    public DbSet<ClassTeacher> ClassTeachers => Set<ClassTeacher>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    async Task<T> IClassRepository.TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await ExecuteWithRetryingTransactionAsync(operation, IsolationLevel.ReadCommitted, ct); }
            catch (DbUpdateException ex) when (attempt < 5 && ex.InnerException is MySqlException
                { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } mysql && mysql.Message.Contains("ux_classes_join_code"))
            { ChangeTracker.Clear(); } // Retry the entire rolled-back operation with a new random code.
            catch (DbUpdateConcurrencyException)
            { throw new DomainRuleViolationException("Lớp đã thay đổi. Vui lòng tải lại.", "CLASS_CONCURRENCY_CONFLICT"); }
        }
    }
    async Task<Class?> IClassRepository.LockAsync(Guid? id, string? code, CancellationToken ct)
    {
        // Materialize before composing: FOR UPDATE must be at the end of the MySQL statement.
        var rows = id.HasValue
            ? await Classes.FromSqlInterpolated($"SELECT * FROM classes WHERE Id = {id.Value} AND IsDeleted = 0 FOR UPDATE").IgnoreQueryFilters().ToListAsync(ct)
            : await Classes.FromSqlInterpolated($"SELECT * FROM classes WHERE JoinCode = {code} AND IsDeleted = 0 FOR UPDATE").IgnoreQueryFilters().ToListAsync(ct);
        var c = rows.SingleOrDefault();
        if (c != null) await Entry(c).Collection(x => x.Teachers).LoadAsync(ct);
        return c;
    }
    Task<bool> IClassRepository.CodeExistsAsync(string code, CancellationToken ct) => Classes.IgnoreQueryFilters().AnyAsync(x => x.JoinCode == code, ct);
    Task<UserRole?> IClassRepository.RoleAsync(Guid id, CancellationToken ct) => Users.Where(x => x.Id == id).Select(x => (UserRole?)x.Role).SingleOrDefaultAsync(ct);
    Task IClassRepository.AddAsync(Class c, CancellationToken ct) => Classes.AddAsync(c, ct).AsTask();
    Task<int> IClassRepository.CountStudentsAsync(Guid id, CancellationToken ct) => ClassEnrollments.CountAsync(x => x.ClassId == id && x.Status == ClassEnrollmentStatus.Active, ct);
    Task<ClassEnrollment?> IClassRepository.EnrollmentAsync(Guid id, Guid student, CancellationToken ct) => ClassEnrollments.SingleOrDefaultAsync(x => x.ClassId == id && x.StudentId == student, ct);
    Task IClassRepository.AddEnrollmentAsync(ClassEnrollment e, CancellationToken ct) => ClassEnrollments.AddAsync(e, ct).AsTask();
    async Task IClassRepository.RemoveAllStudentsAsync(Guid id, CancellationToken ct) => await ClassEnrollments.Where(x => x.ClassId == id)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ClassEnrollmentStatus.Removed), ct);
    Task IClassRepository.AuditAsync(Guid actor, Guid id, string action, Guid? subject, DateTime now, CancellationToken ct) =>
        AuditLogs.AddAsync(AuditLog.Create(actor, AuditAction.ClassChanged, actor, null,
            JsonSerializer.Serialize(new { classId = id, action, subjectId = subject }), now), ct).AsTask();
    async Task IClassRepository.SaveAsync(CancellationToken ct) => await SaveChangesAsync(ct);
    private IQueryable<CodePath.Application.Classes.ClassDto> ClassProjection(IQueryable<Class> q) => q.Select(c => new CodePath.Application.Classes.ClassDto(
        c.Id, c.Name, c.Description, c.Thumbnail, c.Status.ToString(), c.AccessType.ToString(), c.JoinCode,
        c.JoinCodeExpiresAt, c.MaxStudents, c.StartDate, c.EndDate, c.CreatedAt, c.RowVersion,
        c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active),
        c.Teachers.OrderBy(t => t.Teacher.FullName).Select(t => new ClassTeacherDto(t.TeacherId, t.Teacher.FullName, t.Teacher.Email)).ToList(), c.IsTemplate));
    Task<CodePath.Application.Classes.ClassDto?> IClassRepository.DetailAsync(Guid id, CancellationToken ct) =>
        ClassProjection(Classes.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(ct);
    private static IQueryable<Class> FilterClasses(IQueryable<Class> q, ClassFilter f, bool admin)
    {
        if (f.IsTemplate.HasValue) q = q.Where(c => c.IsTemplate == f.IsTemplate.Value);
        if (!string.IsNullOrWhiteSpace(f.Search)) { var s = f.Search.Trim(); var code = s.ToUpperInvariant(); q = q.Where(c => c.Name.Contains(s) || (admin && c.JoinCode != null && c.JoinCode.Contains(code))); }
        if (admin)
        {
            if (f.Status.HasValue) q = q.Where(c => c.Status == f.Status);
            if (f.AccessType.HasValue) q = q.Where(c => c.AccessType == f.AccessType);
            if (f.HasTeacher.HasValue) q = q.Where(c => c.Teachers.Any() == f.HasTeacher);
            if (f.TeacherId.HasValue) q = q.Where(c => c.Teachers.Any(t => t.TeacherId == f.TeacherId));
        }
        return q;
    }
    private static IOrderedQueryable<Class> SortClasses(IQueryable<Class> q, ClassFilter f) => (f.Sort, f.Desc) switch
    {
        ("name", true) => q.OrderByDescending(c => c.Name).ThenBy(c => c.Id),
        ("name", false) => q.OrderBy(c => c.Name).ThenBy(c => c.Id),
        ("studentCount", true) => q.OrderByDescending(c => c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active)).ThenBy(c => c.Id),
        ("studentCount", false) => q.OrderBy(c => c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active)).ThenBy(c => c.Id),
        (_, false) => q.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
        _ => q.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
    };
    async Task<ClassPage<CodePath.Application.Classes.ClassDto>> IClassRepository.ListAsync(ClassFilter f, CancellationToken ct)
    {
        var q = FilterClasses(Classes.AsNoTracking(), f, true);
        var count = await q.CountAsync(ct);
        return new(await ClassProjection(SortClasses(q, f).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)).ToListAsync(ct), f.Page, f.PageSize, count);
    }
    async Task<ClassPage<PublicClassDto>> IClassRepository.PublicAsync(ClassFilter f, Guid student, CancellationToken ct)
    {
        var q = FilterClasses(Classes.AsNoTracking().Where(c => !c.IsTemplate && c.Status == ClassStatus.Active && c.AccessType == ClassAccessType.Public), f, false);
        var count = await q.CountAsync(ct);
        var items = await SortClasses(q, f).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).Select(c => new PublicClassDto(
            c.Id, c.Name, c.Description, c.Thumbnail, c.MaxStudents, c.ClassEnrollments.Count(e => e.Status == ClassEnrollmentStatus.Active),
            c.StartDate, c.EndDate, c.ClassEnrollments.Any(e => e.StudentId == student && e.Status == ClassEnrollmentStatus.Active))).ToListAsync(ct);
        return new(items, f.Page, f.PageSize, count);
    }
    async Task<ClassPage<ClassStudentDto>> IClassRepository.StudentsAsync(Guid id, ClassFilter f, CancellationToken ct)
    {
        var q = ClassEnrollments.AsNoTracking().Where(e => e.ClassId == id && e.Status == ClassEnrollmentStatus.Active);
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(e => e.Student.FullName.Contains(f.Search) || e.Student.Email.Contains(f.Search));
        var count = await q.CountAsync(ct);
        return new(await q.OrderBy(e => e.Student.FullName).ThenBy(e => e.StudentId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .Select(e => new ClassStudentDto(e.StudentId, e.Student.FullName, e.Student.Email, e.JoinedAt, e.JoinMethod.ToString())).ToListAsync(ct), f.Page, f.PageSize, count);
    }
}
