using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Classes;

public sealed record ClassInput(string Name, string Description, string? Thumbnail, ClassStatus Status,
    ClassAccessType AccessType, DateTime? JoinCodeExpiresAt, int? MaxStudents, DateTime? StartDate,
    DateTime? EndDate, Guid[]? TeacherIds, Guid? RowVersion);
public sealed record ClassFilter(int Page = 1, int PageSize = 20, string? Search = null, ClassStatus? Status = null,
    ClassAccessType? AccessType = null, bool? HasTeacher = null, Guid? TeacherId = null, string Sort = "createdAt", bool Desc = true, bool? IsTemplate = false);
public sealed record ClassTeacherDto(Guid Id, string FullName, string Email);
public sealed record ClassDto(Guid Id, string Name, string Description, string? Thumbnail, string Status,
    string AccessType, string? JoinCode, DateTime? JoinCodeExpiresAt, int? MaxStudents,
    DateTime? StartDate, DateTime? EndDate, DateTime CreatedAt, Guid RowVersion, int StudentCount,
    List<ClassTeacherDto> Teachers, bool IsTemplate = false);
// Separate public contract deliberately has no join-code fields.
public sealed record PublicClassDto(Guid Id, string Name, string Description, string? Thumbnail,
    int? MaxStudents, int StudentCount, DateTime? StartDate, DateTime? EndDate, bool IsEnrolled);
public sealed record ClassStudentDto(Guid Id, string FullName, string Email, DateTime JoinedAt, string JoinMethod);
public sealed record ClassPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IClassRepository
{
    Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
    Task<Class?> LockAsync(Guid? id, string? code, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<UserRole?> RoleAsync(Guid id, CancellationToken ct);
    Task AddAsync(Class entity, CancellationToken ct);
    Task<int> CountStudentsAsync(Guid id, CancellationToken ct);
    Task<ClassEnrollment?> EnrollmentAsync(Guid id, Guid student, CancellationToken ct);
    Task AddEnrollmentAsync(ClassEnrollment enrollment, CancellationToken ct);
    Task RemoveAllStudentsAsync(Guid id, CancellationToken ct);
    Task AuditAsync(Guid actor, Guid classId, string action, Guid? subject, DateTime now, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<ClassDto?> DetailAsync(Guid id, CancellationToken ct);
    Task<ClassPage<ClassDto>> ListAsync(ClassFilter filter, CancellationToken ct);
    Task<ClassPage<PublicClassDto>> PublicAsync(ClassFilter filter, Guid student, CancellationToken ct);
    Task<ClassPage<ClassStudentDto>> StudentsAsync(Guid id, ClassFilter filter, CancellationToken ct);
}
