namespace CodePath.Application.Auth.Abstractions;

public interface ILoginConcurrencyLease : IAsyncDisposable
{
}

public sealed record LoginConcurrencyAcquireResult(
    ILoginConcurrencyLease? Lease,
    TimeSpan? RetryAfter)
{
    public bool IsAcquired => Lease is not null;

    public static LoginConcurrencyAcquireResult Acquired(ILoginConcurrencyLease lease) =>
        new(lease, null);

    public static LoginConcurrencyAcquireResult Rejected(TimeSpan retryAfter) =>
        new(null, retryAfter);
}

public interface ILoginConcurrencyLimiter
{
    ValueTask<LoginConcurrencyAcquireResult> AcquireAsync(
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
