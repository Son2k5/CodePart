using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Admin.Abstractions;

public interface IAdminDbContext
{
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
}
