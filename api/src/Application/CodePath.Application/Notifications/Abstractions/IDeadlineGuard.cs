namespace CodePath.Application.Notifications.Abstractions;

/// <summary>
/// Once-per-key idempotency guard used by the scheduled deadline scanner to ensure a given
/// threshold alert (e.g. 24h vs 1h before a topic due date) is emitted at most once, without
/// relying on the notification table's dedup key (which is shared across thresholds).
/// </summary>
public interface IDeadlineGuard
{
    /// <summary>
    /// Atomically claim <paramref name="key"/> for <paramref name="ttl"/>. Returns true when the
    /// caller acquired it (first time) and should proceed; false when it was already claimed.
    /// Implementations fail open (return true) when the backing store is unavailable so a
    /// deadline alert is never silently dropped.
    /// </summary>
    Task<bool> TryAcquireOnceAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Release a previously acquired guard so the next scan can retry after a failure.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
