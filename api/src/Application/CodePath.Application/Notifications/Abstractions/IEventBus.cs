using CodePath.Application.Notifications.Events;

namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Transport-agnostic contract for publishing cross-module integration events.
/// The MVP implementation writes events to the durable outbox (same transaction as
/// the caller's business change); it can later be swapped for Kafka/RabbitMQ without
/// touching producers. Producers must call this within a unit of work that persists
/// via <c>SaveChangesAsync</c> so the event is committed atomically with the change.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}
