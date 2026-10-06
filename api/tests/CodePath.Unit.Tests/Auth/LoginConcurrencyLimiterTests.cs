using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.LoginProtection;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class LoginConcurrencyLimiterTests
{
    [Fact]
    public async Task AcquireAsync_ShouldNeverExceedGlobalLimit()
    {
        using var limiter = CreateLimiter(maxGlobal: 2, maxPerIp: 2, queueLimit: 2);
        var first = await limiter.AcquireAsync("192.0.2.1");
        var second = await limiter.AcquireAsync("192.0.2.2");

        var thirdTask = limiter.AcquireAsync("192.0.2.3").AsTask();
        await Task.Delay(100);
        thirdTask.IsCompleted.Should().BeFalse();

        await first.Lease!.DisposeAsync();
        var third = await thirdTask;
        third.IsAcquired.Should().BeTrue();

        await second.Lease!.DisposeAsync();
        await third.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_ShouldLimitOneIpWithoutBlockingAnotherIp()
    {
        using var limiter = CreateLimiter(maxGlobal: 2, maxPerIp: 1, queueLimit: 3);
        var first = await limiter.AcquireAsync("192.0.2.1");

        var sameIpTask = limiter.AcquireAsync("192.0.2.1").AsTask();
        var otherIp = await limiter.AcquireAsync("192.0.2.2");

        otherIp.IsAcquired.Should().BeTrue();
        sameIpTask.IsCompleted.Should().BeFalse();

        await first.Lease!.DisposeAsync();
        var sameIp = await sameIpTask;
        sameIp.IsAcquired.Should().BeTrue();

        await otherIp.Lease!.DisposeAsync();
        await sameIp.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueIsFull_ShouldRejectImmediately()
    {
        using var limiter = CreateLimiter(maxGlobal: 1, maxPerIp: 1, queueLimit: 0);
        var first = await limiter.AcquireAsync("192.0.2.1");

        var rejected = await limiter.AcquireAsync("192.0.2.2");

        rejected.IsAcquired.Should().BeFalse();
        rejected.RetryAfter.Should().BePositive();
        await first.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenWaitTimesOut_ShouldReject()
    {
        using var limiter = CreateLimiter(
            maxGlobal: 1,
            maxPerIp: 1,
            queueLimit: 1,
            timeoutSeconds: 1);
        var first = await limiter.AcquireAsync("192.0.2.1");

        var rejected = await limiter.AcquireAsync("192.0.2.2");

        rejected.IsAcquired.Should().BeFalse();
        rejected.RetryAfter.Should().Be(TimeSpan.FromSeconds(1));
        await first.Lease!.DisposeAsync();
    }

    [Fact]
    public async Task OneIpWaitingOnItsOwnGate_ShouldNotConsumeTheGlobalQueue()
    {
        using var limiter = CreateLimiter(
            maxGlobal: 1,
            maxPerIp: 1,
            queueLimit: 1,
            timeoutSeconds: 5);
        var attackRunning = await limiter.AcquireAsync("192.0.2.1");
        var attackWaiting = limiter.AcquireAsync("192.0.2.1").AsTask();
        await Task.Delay(50);

        var realUserWaiting = limiter.AcquireAsync("192.0.2.2").AsTask();
        await Task.Delay(50);

        realUserWaiting.IsCompleted.Should().BeFalse(
            "the same-IP waiter must remain outside the global queue");

        await attackRunning.Lease!.DisposeAsync();
        var realUser = await realUserWaiting.WaitAsync(TimeSpan.FromSeconds(2));
        realUser.IsAcquired.Should().BeTrue();
        await realUser.Lease!.DisposeAsync();

        var secondAttack = await attackWaiting.WaitAsync(TimeSpan.FromSeconds(2));
        secondAttack.IsAcquired.Should().BeTrue();
        await secondAttack.Lease!.DisposeAsync();
    }

    private static LoginConcurrencyLimiter CreateLimiter(
        int maxGlobal,
        int maxPerIp,
        int queueLimit,
        int timeoutSeconds = 5) =>
        new(Options.Create(new LoginConcurrencyOptions
        {
            MaxConcurrentLogins = maxGlobal,
            MaxConcurrentPerIp = maxPerIp,
            QueueLimit = queueLimit,
            QueueTimeoutSeconds = timeoutSeconds
        }));
}
