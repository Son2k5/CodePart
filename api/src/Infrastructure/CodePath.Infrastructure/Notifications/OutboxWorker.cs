using System.Text.Json;
using CodePath.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Drains the transactional outbox. Claims pending rows with a time-boxed lease, re-publishes
/// each integration event through MediatR (which invokes the notification handlers), then marks
/// the row processed. Failures are retried with exponential backoff and dead-lettered after
/// <see cref="MaxAttempts"/>. Mirrors the lease + backoff shape of <c>RagIndexWorker</c>.
/// </summary>
public sealed class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> logger) : BackgroundService
{
    internal const int MaxAttempts = 5;
    internal const int BatchSize = 20;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

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
                logger.LogWarning("Outbox worker will retry ({ErrorType}).", e.GetType().Name);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task Tick(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var now = DateTime.UtcNow;

        var candidates = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null
                && m.NextAttemptAtUtc <= now && (m.LeaseUntilUtc == null || m.LeaseUntilUtc < now))
            .OrderBy(m => m.NextAttemptAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in candidates)
        {
            var lease = Guid.NewGuid();
            var claimed = await db.OutboxMessages
                .Where(m => m.Id == row.Id && m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null
                    && (m.LeaseUntilUtc == null || m.LeaseUntilUtc < now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.LeaseToken, lease)
                    .SetProperty(m => m.LeaseUntilUtc, now.Add(LeaseDuration))
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
            if (claimed != 1) continue;

            var attempts = row.Attempts + 1;
            try
            {
                await DispatchAsync(publisher, row.EventType, row.Payload, ct);

                await db.OutboxMessages
                    .Where(m => m.Id == row.Id && m.LeaseToken == lease)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.ProcessedAtUtc, DateTime.UtcNow)
                        .SetProperty(m => m.Error, (string?)null)
                        .SetProperty(m => m.LeaseToken, (Guid?)null)
                        .SetProperty(m => m.LeaseUntilUtc, (DateTime?)null), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var deadLettered = attempts >= MaxAttempts;
                var backoff = TimeSpan.FromSeconds(Math.Min(300, 10 * Math.Pow(2, attempts)));
                var error = ex is InvalidOperationException ? ex.Message : ex.GetType().Name;
                var nextAttemptAt = DateTime.UtcNow.Add(backoff);

                await db.OutboxMessages
                    .Where(m => m.Id == row.Id && m.LeaseToken == lease)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Error, error)
                        .SetProperty(m => m.DeadLetteredAtUtc, deadLettered ? DateTime.UtcNow : (DateTime?)null)
                        .SetProperty(m => m.NextAttemptAtUtc, nextAttemptAt)
                        .SetProperty(m => m.LeaseToken, (Guid?)null)
                        .SetProperty(m => m.LeaseUntilUtc, (DateTime?)null), ct);

                logger.LogWarning(ex, "Outbox message {MessageId} ({EventType}) failed (attempt {Attempt}, dead={Dead}).",
                    row.Id, row.EventType, attempts, deadLettered);
            }
        }
    }

    private static async Task DispatchAsync(IPublisher publisher, string eventType, string payload, CancellationToken ct)
    {
        var type = Type.GetType(eventType)
            ?? throw new InvalidOperationException($"Outbox event type '{eventType}' could not be resolved.");

        var evt = JsonSerializer.Deserialize(payload, type, OutboxEventBus.SerializerOptions)
            ?? throw new InvalidOperationException($"Outbox payload for '{eventType}' deserialized to null.");

        await publisher.Publish(evt, ct);
    }
}
