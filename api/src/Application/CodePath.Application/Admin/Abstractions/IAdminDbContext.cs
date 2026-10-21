using CodePath.Domain.Auditing.Entities;
using CodePath.Application.Admin.Dtos;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Admin.Abstractions;

public interface IAdminDbContext
{
    Task<OffsetPage<TeacherDto>> GetPagedTeachersAsync(TeacherFilter filter, CancellationToken cancellationToken);
    Task<TeacherStatsDto> GetTeacherStatsAsync(CancellationToken cancellationToken);
    Task<TeacherDetailDto?> GetTeacherDetailByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FacultyDto>> GetFacultiesAsync(CancellationToken cancellationToken);
    Task<bool> FacultyExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TeacherCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<OffsetPage<StudentDto>> GetPagedStudentsAsync(StudentFilter filter, CancellationToken cancellationToken);
    Task<StudentStatsDto> GetStudentStatsAsync(CancellationToken cancellationToken);
    Task<StudentDetailDto?> GetStudentDetailByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> StudentCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetTeachersAsync(
        UserStatus? status,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(
        Guid? targetUserId,
        DateTime? cursorOccurredAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteUserAsync(Guid actorId, Guid targetUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
