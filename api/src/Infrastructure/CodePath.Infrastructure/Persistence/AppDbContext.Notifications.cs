using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Learning;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    Task INotificationDbContext.AddNotificationAsync(Notification notification, CancellationToken cancellationToken)
        => Notifications.AddAsync(notification, cancellationToken).AsTask();

    Task INotificationDbContext.AddNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken)
        => Notifications.AddRangeAsync(notifications, cancellationToken);

    Task<Notification?> INotificationDbContext.GetForRecipientAsync(Guid notificationId, Guid recipientId, CancellationToken cancellationToken)
        => Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == recipientId, cancellationToken);

    Task INotificationDbContext.RemoveAsync(Notification notification, CancellationToken cancellationToken)
    {
        Notifications.Remove(notification);
        return Task.CompletedTask;
    }

    Task<bool> INotificationDbContext.DedupExistsAsync(Guid recipientId, string dedupKey, CancellationToken cancellationToken)
        => Notifications.AnyAsync(n => n.RecipientId == recipientId && n.DedupKey == dedupKey, cancellationToken);

    Task<int> INotificationDbContext.CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken)
        => Notifications.CountAsync(n => n.RecipientId == recipientId && !n.IsRead, cancellationToken);

    async Task<IReadOnlyList<Notification>> INotificationDbContext.QueryAsync(
        Guid recipientId,
        bool? isRead,
        NotificationType? type,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = Notifications.AsNoTracking().Where(n => n.RecipientId == recipientId);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
        {
            query = query.Where(n =>
                n.CreatedAt < cursorCreatedAt.Value
                || (n.CreatedAt == cursorCreatedAt.Value && n.Id.CompareTo(cursorId.Value) < 0));
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows;
    }

    Task<int> INotificationDbContext.MarkAllReadAsync(Guid recipientId, DateTime utcNow, CancellationToken cancellationToken)
        => Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, utcNow)
                .SetProperty(n => n.UpdatedAt, utcNow),
                cancellationToken);

    Task<NotificationPreference?> INotificationDbContext.GetPreferenceAsync(Guid userId, NotificationType type, CancellationToken cancellationToken)
        => NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type, cancellationToken);

    async Task<IReadOnlyList<NotificationPreference>> INotificationDbContext.GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
        => await NotificationPreferences.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken);

    Task INotificationDbContext.AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken)
        => NotificationPreferences.AddAsync(preference, cancellationToken).AsTask();

    async Task<IReadOnlyList<Guid>> INotificationDbContext.GetClassStudentIdsAsync(Guid classId, CancellationToken cancellationToken)
        => await ClassEnrollments.AsNoTracking()
            .Where(e => e.ClassId == classId && e.Status == ClassEnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken);

    async Task<IReadOnlyList<Guid>> INotificationDbContext.GetClassTeacherIdsAsync(Guid classId, CancellationToken cancellationToken)
        => await ClassTeachers.AsNoTracking()
            .Where(t => t.ClassId == classId)
            .Select(t => t.TeacherId)
            .ToListAsync(cancellationToken);

    async Task<IReadOnlyList<DeadlineCandidate>> INotificationDbContext.GetDeadlineCandidatesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var windowStart = now - TimeSpan.FromHours(48);
        var windowEnd = now + TimeSpan.FromHours(24);

        var rows = await (
            from e in ClassEnrollments.AsNoTracking()
            where e.Status == ClassEnrollmentStatus.Active
            join c in Classes.AsNoTracking() on e.ClassId equals c.Id
            where !c.IsDeleted && !c.IsTemplate && c.Status == ClassStatus.Active && c.Mode == ClassMode.Managed
            join t in Topics.AsNoTracking() on c.Id equals t.ClassId
            where !t.IsDeleted && t.Status == TopicStatus.Published && t.DueAt != null
                && t.DueAt >= windowStart && t.DueAt <= windowEnd
            join tp in TopicProgress.AsNoTracking()
                on new { u = e.StudentId, t = t.Id } equals new { u = tp.UserId, t = tp.TopicId } into progressGroup
            from tp in progressGroup.DefaultIfEmpty()
            select new
            {
                StudentId = e.StudentId,
                TopicId = t.Id,
                ClassId = t.ClassId,
                t.Title,
                Due = t.DueAt!.Value,
                Extension = tp == null ? (DateTime?)null : tp.DueExtensionAt,
                Completed = tp != null && tp.Status == LearningStatus.Completed
            }).ToListAsync(cancellationToken);

        return rows
            .Select(r => new DeadlineCandidate(r.StudentId, r.TopicId, r.ClassId, r.Title, r.Extension ?? r.Due, r.Completed))
            .Where(c => !c.Completed && c.EffectiveDueAt >= windowStart && c.EffectiveDueAt <= windowEnd)
            .GroupBy(c => (c.StudentId, c.TopicId))
            .Select(g => g.First())
            .ToList();
    }

    async Task<IReadOnlyList<Guid>> INotificationDbContext.GetUserIdsByRoleAsync(UserRole role, CancellationToken cancellationToken)
        => await Users.AsNoTracking()
            .Where(u => u.Role == role && u.Status == UserStatus.Active)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    async Task<IReadOnlyList<Guid>> INotificationDbContext.GetAllActiveUserIdsAsync(CancellationToken cancellationToken)
        => await Users.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    async Task<string?> INotificationDbContext.GetUserEmailAsync(Guid userId, CancellationToken cancellationToken)
        => await Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

    Task<int> INotificationDbContext.DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        => Notifications.Where(n => n.CreatedAt < cutoffUtc).ExecuteDeleteAsync(cancellationToken);

    Task INotificationDbContext.AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        => AuditLogs.AddAsync(auditLog, cancellationToken).AsTask();

    Task INotificationDbContext.AddOutboxAsync(OutboxMessage message, CancellationToken cancellationToken)
        => OutboxMessages.AddAsync(message, cancellationToken).AsTask();
}
