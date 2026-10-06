using System.Collections.Concurrent;
using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.LoginProtection;

public sealed class LoginConcurrencyLimiter : ILoginConcurrencyLimiter, IDisposable
{
    private readonly LoginConcurrencyOptions _options;
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly ConcurrentDictionary<string, PerIpEntry> _perIp = new(StringComparer.Ordinal);
    private int _requestsInGate;
    private bool _disposed;

    public LoginConcurrencyLimiter(IOptions<LoginConcurrencyOptions> options)
    {
        _options = options.Value;
        _globalSemaphore = new SemaphoreSlim(
            _options.MaxConcurrentLogins,
            _options.MaxConcurrentLogins);
    }

    public async ValueTask<LoginConcurrencyAcquireResult> AcquireAsync(
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = NormalizeIp(ipAddress);
        var perIpEntry = RentPerIpEntry(key, out var perIpRequests);
        if (perIpRequests > checked(_options.MaxConcurrentPerIp + _options.QueueLimit))
        {
            ReturnPerIpEntry(key, perIpEntry);
            return LoginConcurrencyAcquireResult.Rejected(TimeSpan.FromSeconds(1));
        }

        var perIpAcquired = false;
        var globalAdmitted = false;
        var globalAcquired = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.QueueTimeoutSeconds));

        try
        {
            await perIpEntry.Semaphore.WaitAsync(timeout.Token);
            perIpAcquired = true;

            var admitted = Interlocked.Increment(ref _requestsInGate);
            globalAdmitted = true;
            if (admitted > checked(_options.MaxConcurrentLogins + _options.QueueLimit))
            {
                Interlocked.Decrement(ref _requestsInGate);
                globalAdmitted = false;
                perIpEntry.Semaphore.Release();
                perIpAcquired = false;
                ReturnPerIpEntry(key, perIpEntry);
                return LoginConcurrencyAcquireResult.Rejected(TimeSpan.FromSeconds(1));
            }

            await _globalSemaphore.WaitAsync(timeout.Token);
            globalAcquired = true;

            return LoginConcurrencyAcquireResult.Acquired(
                new Lease(this, key, perIpEntry));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (globalAcquired)
            {
                _globalSemaphore.Release();
            }

            if (perIpAcquired)
            {
                perIpEntry.Semaphore.Release();
            }

            ReturnPerIpEntry(key, perIpEntry);
            if (globalAdmitted)
            {
                Interlocked.Decrement(ref _requestsInGate);
            }
            return LoginConcurrencyAcquireResult.Rejected(
                TimeSpan.FromSeconds(_options.QueueTimeoutSeconds));
        }
        catch
        {
            if (globalAcquired)
            {
                _globalSemaphore.Release();
            }

            if (perIpAcquired)
            {
                perIpEntry.Semaphore.Release();
            }

            ReturnPerIpEntry(key, perIpEntry);
            if (globalAdmitted)
            {
                Interlocked.Decrement(ref _requestsInGate);
            }
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _globalSemaphore.Dispose();
        foreach (var entry in _perIp.Values)
        {
            entry.Semaphore.Dispose();
        }
    }

    private PerIpEntry RentPerIpEntry(string key, out int references)
    {
        while (true)
        {
            var entry = _perIp.GetOrAdd(
                key,
                _ => new PerIpEntry(_options.MaxConcurrentPerIp));

            lock (entry)
            {
                if (entry.Removed)
                {
                    continue;
                }

                entry.References++;
                references = entry.References;
                return entry;
            }
        }
    }

    private void ReturnPerIpEntry(string key, PerIpEntry entry)
    {
        lock (entry)
        {
            entry.References--;
            if (entry.References != 0)
            {
                return;
            }

            entry.Removed = true;
            _perIp.TryRemove(new KeyValuePair<string, PerIpEntry>(key, entry));
            entry.Semaphore.Dispose();
        }
    }

    private void Release(string key, PerIpEntry entry)
    {
        _globalSemaphore.Release();
        entry.Semaphore.Release();
        Interlocked.Decrement(ref _requestsInGate);
        ReturnPerIpEntry(key, entry);
    }

    private static string NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown"
            : ipAddress.Trim().ToLowerInvariant();

    private sealed class PerIpEntry
    {
        public PerIpEntry(int permitLimit) => Semaphore = new SemaphoreSlim(permitLimit, permitLimit);

        public SemaphoreSlim Semaphore { get; }
        public int References { get; set; }
        public bool Removed { get; set; }
    }

    private sealed class Lease : ILoginConcurrencyLease
    {
        private LoginConcurrencyLimiter? _owner;
        private readonly string _key;
        private readonly PerIpEntry _entry;

        public Lease(LoginConcurrencyLimiter owner, string key, PerIpEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(_key, _entry);
            return ValueTask.CompletedTask;
        }
    }
}
