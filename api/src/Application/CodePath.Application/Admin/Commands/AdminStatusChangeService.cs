using System.Text.Json;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Shared.Kernel.Common;

namespace CodePath.Application.Admin.Commands;

internal enum AdminStatusAction
{
    ApproveTeacher,
    RejectTeacher,
    DisableUser
}

internal sealed class AdminStatusChangeService
{
    private readonly IAdminDbContext _adminDbContext;
    private readonly IUsersDbContext _usersDbContext;
    private readonly IAuthDbContext _authDbContext;
    private readonly TimeProvider _timeProvider;

    public AdminStatusChangeService(
        IAdminDbContext adminDbContext,
        IUsersDbContext usersDbContext,
        IAuthDbContext authDbContext,
        TimeProvider timeProvider)
    {
        _adminDbContext = adminDbContext;
        _usersDbContext = usersDbContext;
        _authDbContext = authDbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminUserStatusChangeDto>> ChangeAsync(
        Guid actorId,
        Guid targetUserId,
        AdminStatusAction requestedAction,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await _adminDbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var target = await _usersDbContext.GetByIdAsync(targetUserId, transactionCancellationToken);
            if (target is null)
                return Result<AdminUserStatusChangeDto>.Failure("User not found.", ErrorCodes.NotFound);

            var previousStatus = target.Status;
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            if (requestedAction == AdminStatusAction.ApproveTeacher
                && !target.EmailVerifiedAt.HasValue)
            {
                return Result<AdminUserStatusChangeDto>.Failure(
                    "A teacher must verify their email before approval.",
                    ErrorCodes.Conflict);
            }

            var auditAction = requestedAction switch
            {
                AdminStatusAction.ApproveTeacher => Approve(target, utcNow),
                AdminStatusAction.RejectTeacher => Reject(target, utcNow),
                AdminStatusAction.DisableUser => Disable(target, actorId, utcNow),
                _ => throw new ArgumentOutOfRangeException(nameof(requestedAction))
            };

            var revokedSessionCount = requestedAction == AdminStatusAction.ApproveTeacher
                ? 0
                : await _authDbContext.RevokeAllUserRefreshTokensAsync(
                    target.Id,
                    utcNow,
                    ipAddress,
                    transactionCancellationToken);

            // The allow-list below is intentional: arbitrary request/entity data must never
            // enter audit JSON (especially password hashes, access tokens or refresh tokens).
            var safeAuditData = JsonSerializer.Serialize(new
            {
                fromStatus = previousStatus.ToString(),
                toStatus = target.Status.ToString(),
                role = target.Role.ToString(),
                revokedSessionCount
            });

            var auditLog = AuditLog.Create(
                actorId,
                auditAction,
                target.Id,
                ipAddress,
                safeAuditData,
                utcNow);

            await _adminDbContext.AddAuditLogAsync(auditLog, transactionCancellationToken);
            await _usersDbContext.SaveChangesAsync(transactionCancellationToken);

            return Result<AdminUserStatusChangeDto>.Success(new(
                target.Id,
                target.Status.ToString(),
                revokedSessionCount,
                auditLog.Id));
        }, cancellationToken);
    }

    private static AuditAction Approve(Domain.Users.Entities.User user, DateTime utcNow)
    {
        user.ApproveTeacher(utcNow);
        return AuditAction.TeacherApproved;
    }

    private static AuditAction Reject(Domain.Users.Entities.User user, DateTime utcNow)
    {
        user.RejectTeacher(utcNow);
        return AuditAction.TeacherRejected;
    }

    private static AuditAction Disable(Domain.Users.Entities.User user, Guid actorId, DateTime utcNow)
    {
        user.Disable(actorId, utcNow);
        return AuditAction.UserDisabled;
    }
}
