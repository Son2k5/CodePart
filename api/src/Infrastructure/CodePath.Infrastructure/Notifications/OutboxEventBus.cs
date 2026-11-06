using System.Text.Json;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Events;
using CodePath.Domain.Notifications.Entities;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// <see cref="IEventBus"/> implementation backed by the transactional outbox. Events are
/// serialized and staged onto <c>outbox_messages</c> without their own SaveChanges, so they
/// commit inside the producer's business transaction. The <see cref="OutboxWorker"/> drains
/// them afterwards and re-publishes through MediatR.
/// </summary>
public sealed class OutboxEventBus(INotificationDbContext db, TimeProvider clock) : IEventBus
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var eventType = typeof(TEvent).AssemblyQualifiedName
                        ?? throw new InvalidOperationException($"Cannot resolve assembly-qualified name for {typeof(TEvent).Name}.");

        var payload = JsonSerializer.Serialize(@event, typeof(TEvent), SerializerOptions);

        var message = OutboxMessage.Create(
            eventType,
            payload,
            @event.OccurredAtUtc == default ? clock.GetUtcNow().UtcDateTime : @event.OccurredAtUtc);

        await db.AddOutboxAsync(message, cancellationToken);
    }
}
