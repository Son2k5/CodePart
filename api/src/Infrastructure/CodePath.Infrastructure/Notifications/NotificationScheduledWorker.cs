using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Notifications.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Scheduled maintenance for the notification module. Each tick scans topic deadlines and, once a
/// day, prunes aged notifications. Mirrors the <see cref="OutboxWorker"/> shape (BackgroundService +
/// a fresh DI scope per tick) and honors the minimal-libs principle by polling rather than using a
/// scheduler package.
/// </summary>
public sealed class NotificationScheduledWorker(IServiceScopeFactory scopes, ILogger<NotificationScheduledWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan Soon24Window = TimeSpan.FromHours(24);
    internal static readonly TimeSpan Soon1Window = TimeSpan.FromHours(1);
    internal static readonly TimeSpan PassedWindow = TimeSpan.FromHours(48);
    internal static readonly TimeSpan GuardTtl = TimeSpan.FromDays(35);
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    internal static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    // In-memory so cleanup runs at most once per day per instance; multi-replica duplicates are
    // harmless (ExecuteDeleteAsync is idempotent).
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Tick(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogWarning("Notification scheduled worker will retry ({ErrorType}).", e.GetType().Name);
            }

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task Tick(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<INotificationDbContext>();
        var notifications = provider.GetRequiredService<INotificationService>();
        var guard = provider.GetRequiredService<IDeadlineGuard>();
        var clock = provider.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow().UtcDateTime;

        await ScanDeadlinesAsync(db, notifications, guard, now, ct);

        if (_lastCleanupUtc == DateTime.MinValue || now - _lastCleanupUtc >= CleanupInterval)
        {
            var deleted = await db.DeleteOlderThanAsync(now - Retention, ct);
            _lastCleanupUtc = now;
            if (deleted > 0)
                logger.LogInformation("Notification cleanup removed {Deleted} row(s) older than {Days} days.", deleted, (int)Retention.TotalDays);
        }
    }

    private async Task ScanDeadlinesAsync(INotificationDbContext db, INotificationService notifications, IDeadlineGuard guard, DateTime now, CancellationToken ct)
    {
        var candidates = await db.GetDeadlineCandidatesAsync(now, ct);
        foreach (var candidate in candidates)
        {
            if (ct.IsCancellationRequested) break;

            var remaining = candidate.EffectiveDueAt - now;
            string tag;
            NotificationType type;
            string title;
            string body;
            if (remaining <= TimeSpan.Zero && remaining >= -PassedWindow)
            {
                tag = "passed";
                type = NotificationType.TopicDeadlinePassed;
                title = "Assignment deadline passed";
                body = $"The deadline for \"{candidate.Title}\" has passed.";
            }
            else if (remaining > TimeSpan.Zero && remaining <= Soon1Window)
            {
                tag = "soon1";
                type = NotificationType.TopicDeadlineSoon;
                title = "Deadline approaching";
                body = $"\"{candidate.Title}\" is due within an hour.";
            }
            else if (remaining > Soon1Window && remaining <= Soon24Window)
            {
                tag = "soon24";
                type = NotificationType.TopicDeadlineSoon;
                title = "Deadline approaching";
                body = $"\"{candidate.Title}\" is due within 24 hours.";
            }
            else
            {
                continue;
            }

            // EntityId is intentionally null: the DB dedup key is type:recipient:entityId, so the
            // 24h and 1h reminders (same type) must not collide. Redis enforces once-per-threshold.
            var guardKey = $"notifications:deadline:{candidate.StudentId:N}:{candidate.TopicId:N}:{tag}";
            var acquired = await guard.TryAcquireOnceAsync(guardKey, GuardTtl, ct);
            if (!acquired) continue;

            try
            {
                await notifications.SendAsync(new NotificationRequest(
                    candidate.StudentId, type, title, body,
                    EntityId: null,
                    Payload: new Dictionary<string, object?>
                    {
                        ["topicId"] = candidate.TopicId,
                        ["classId"] = candidate.ClassId,
                        ["dueAt"] = candidate.EffectiveDueAt,
                    }), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Release the guard so the next tick can retry a delivery that failed.
                logger.LogWarning(ex, "Deadline notification ({Tag}) delivery failed for student {StudentId}, topic {TopicId}.",
                    tag, candidate.StudentId, candidate.TopicId);
                await guard.ReleaseAsync(guardKey, CancellationToken.None);
            }
        }
    }
}
