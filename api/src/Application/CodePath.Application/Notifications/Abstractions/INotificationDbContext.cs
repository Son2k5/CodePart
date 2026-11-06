using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Data access surface for the notification module, implemented by AppDbContext
/// following the same per-module interface pattern as IAdminDbContext / IUsersDbContext.
/// </summary>
public interface INotificationDbContext
{
    Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task AddNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    Task<Notification?> GetForRecipientAsync(Guid notificationId, Guid recipientId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<bool> DedupExistsAsync(Guid recipientId, string dedupKey, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> QueryAsync(
        Guid recipientId,
        bool? isRead,
        NotificationType? type,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(Guid recipientId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<NotificationPreference?> GetPreferenceAsync(Guid userId, NotificationType type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetClassStudentIdsAsync(Guid classId, CancellationToken cancellationToken = default);

    /// <summary>Teachers assigned to a class (the authoritative learning model).</summary>
    Task<IReadOnlyList<Guid>> GetClassTeacherIdsAsync(Guid classId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrolled-student deadline rows for the scheduled scanner: published topics with a due date in
    /// an active, managed (non-template) class, within <c>[now-48h, now+24h]</c>, with the per-student
    /// extension applied and whether the student has already completed the topic.
    /// </summary>
    Task<IReadOnlyList<DeadlineCandidate>> GetDeadlineCandidatesAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetAllActiveUserIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves a recipient's email address for the email channel. Returns null when unknown.</summary>
    Task<string?> GetUserEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>Stages an audit-log row for the current unit of work (e.g. an admin broadcast).</summary>
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an integration event onto the durable outbox WITHOUT saving. The row is
    /// committed by the caller's surrounding unit of work (SaveChangesAsync), guaranteeing
    /// the event and the business change land in the same transaction.
    /// </summary>
    Task AddOutboxAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
