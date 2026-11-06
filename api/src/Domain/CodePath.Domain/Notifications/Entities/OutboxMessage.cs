namespace CodePath.Domain.Notifications.Entities;

/// <summary>
/// Transactional outbox row: committed in the same transaction as the business operation
/// that produced it, then drained asynchronously by a background worker for at-least-once
/// delivery of cross-module integration events. Follows the same lease + exponential-backoff
/// pattern as <see cref="CodePath.Domain.Learning.RagIndexJob"/>.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Assembly-qualified name of the integration event CLR type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>JSON-serialized event payload.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? DeadLetteredAtUtc { get; set; }
    public string? Error { get; set; }

    public static OutboxMessage Create(string eventType, string payload, DateTime utcNow) => new()
    {
        EventType = eventType,
        Payload = payload,
        OccurredAtUtc = utcNow,
        NextAttemptAtUtc = utcNow,
    };

    public bool IsPending => ProcessedAtUtc is null && DeadLetteredAtUtc is null;
}
