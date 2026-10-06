using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.LoginProtection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class LoginAttemptProtectionTests
{
    [Fact]
    public async Task PairCounter_ShouldBackoffExponentially_WithoutAffectingAnotherEmail()
    {
        var time = new ManualTimeProvider();
        var service = CreateInMemoryService(time);

        for (var attempt = 1; attempt < 5; attempt++)
        {
            var decision = await service.RecordFailureAsync("first@example.edu", "192.0.2.1");
            decision.IsInBackoff.Should().BeFalse();
        }

        var fifth = await service.RecordFailureAsync("first@example.edu", "192.0.2.1");
        fifth.RetryAfter.Should().Be(TimeSpan.FromSeconds(2));

        var otherEmail = await service.GetDecisionAsync("second@example.edu", "192.0.2.1");
        otherEmail.IsInBackoff.Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(3));
        var sixth = await service.RecordFailureAsync("first@example.edu", "192.0.2.1");
        sixth.RetryAfter.Should().Be(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task RedisTimeout_ShouldUseInMemoryFallbackInsteadOfFailingOpen()
    {
        var options = Options.Create(CreateOptions());
        var fallback = new InMemoryLoginAttemptService(options, TimeProvider.System);
        var database = new Mock<IDatabase>();
        database
            .Setup(database => database.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new TimeoutException("Redis unavailable"));
        var connection = new Mock<IConnectionMultiplexer>();
        connection
            .Setup(multiplexer => multiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var service = new RedisLoginAttemptService(
            connection.Object,
            fallback,
            options,
            NullLogger<RedisLoginAttemptService>.Instance);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await service.RecordFailureAsync("fallback@example.edu", "192.0.2.10");
        }

        var decision = await service.GetDecisionAsync("fallback@example.edu", "192.0.2.10");
        decision.IsInBackoff.Should().BeTrue();
    }

    private static InMemoryLoginAttemptService CreateInMemoryService(TimeProvider timeProvider) =>
        new(Options.Create(CreateOptions()), timeProvider);

    private static LoginSecurityOptions CreateOptions() => new()
    {
        BackoffThreshold = 5,
        FailureCounterTtlMinutes = 15,
        InitialBackoffSeconds = 2,
        MaxBackoffSeconds = 8
    };

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan value) => _utcNow = _utcNow.Add(value);
    }
}
