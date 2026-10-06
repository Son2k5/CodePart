using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.LoginProtection;

public sealed class InMemoryLoginAttemptService : ILoginAttemptService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PairState> _pairs = new(StringComparer.Ordinal);
    private readonly LoginSecurityOptions _options;
    private readonly TimeProvider _timeProvider;

    public InMemoryLoginAttemptService(IOptions<LoginSecurityOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task<LoginAttemptDecision> GetDecisionAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var pairKey = GetPairKey(normalizedEmail, ipAddress);
            RemoveExpired(pairKey, now);

            TimeSpan? retryAfter = _pairs.TryGetValue(pairKey, out var pair) && pair.BackoffUntil > now
                ? pair.BackoffUntil - now
                : null;
            return Task.FromResult(new LoginAttemptDecision(retryAfter));
        }
    }

    public Task<LoginAttemptDecision> RecordFailureAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var pairKey = GetPairKey(normalizedEmail, ipAddress);
            RemoveExpired(pairKey, now);

            if (!_pairs.TryGetValue(pairKey, out var pair))
            {
                pair = new PairState();
                _pairs[pairKey] = pair;
            }

            pair.Failures++;
            pair.ExpiresAt = now.AddMinutes(_options.FailureCounterTtlMinutes);

            TimeSpan? retryAfter = null;
            if (pair.Failures >= _options.BackoffThreshold)
            {
                retryAfter = TimeSpan.FromSeconds(CalculateBackoffSeconds(pair.Failures));
                pair.BackoffUntil = now.Add(retryAfter.Value);
            }

            return Task.FromResult(new LoginAttemptDecision(retryAfter));
        }
    }

    public Task ResetAsync(
        string normalizedEmail,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _pairs.Remove(GetPairKey(normalizedEmail, ipAddress));
        }

        return Task.CompletedTask;
    }

    private int CalculateBackoffSeconds(int failures)
    {
        var exponent = Math.Min(30, failures - _options.BackoffThreshold);
        var multiplier = 1L << exponent;
        return (int)Math.Min(_options.MaxBackoffSeconds, _options.InitialBackoffSeconds * multiplier);
    }

    private void RemoveExpired(string pairKey, DateTimeOffset now)
    {
        if (_pairs.TryGetValue(pairKey, out var pair) && pair.ExpiresAt <= now)
        {
            _pairs.Remove(pairKey);
        }
    }

    private static string GetPairKey(string email, string? ipAddress) =>
        $"{NormalizeIp(ipAddress)}:{NormalizeEmail(email)}";

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim().ToLowerInvariant();

    private sealed class PairState
    {
        public int Failures { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset BackoffUntil { get; set; }
    }
}
