namespace CodePath.Shared.Kernel.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}
