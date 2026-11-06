using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Domain.Notifications.Enums;
using CodePath.Infrastructure.Notifications;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Notifications;

public sealed class NotificationScheduledWorkerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<INotificationDbContext> _db = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IDeadlineGuard> _guard = new();
    private readonly List<NotificationRequest> _sent = new();

    public NotificationScheduledWorkerTests()
    {
        _guard.Setup(g => g.TryAcquireOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _db.Setup(d => d.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _notifications.Setup(n => n.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => _sent.Add(r))
            .ReturnsAsync(Result<NotificationDto?>.Success(null));
    }

    private NotificationScheduledWorker BuildWorker()
    {
        var provider = new ServiceCollection()
            .AddSingleton(_db.Object)
            .AddSingleton(_notifications.Object)
            .AddSingleton(_guard.Object)
            .AddSingleton<TimeProvider>(new FixedClock(Now))
            .BuildServiceProvider();
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(s => s.CreateScope()).Returns(scope.Object);
        return new NotificationScheduledWorker(scopes.Object, NullLogger<NotificationScheduledWorker>.Instance);
    }

    private void SetupCandidates(params DeadlineCandidate[] candidates)
        => _db.Setup(d => d.GetDeadlineCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates.ToList());

    [Fact]
    public async Task Tick_EmitsSoon24_Soon1_AndPassed_AndRunsCleanup()
    {
        SetupCandidates(
            Candidate("A", Now + TimeSpan.FromHours(24)),
            Candidate("B", Now + TimeSpan.FromMinutes(30)),
            Candidate("C", Now - TimeSpan.FromMinutes(10)));

        await BuildWorker().Tick(default);

        _sent.Should().HaveCount(3);
        _sent.Should().ContainSingle(s => s.Type == NotificationType.TopicDeadlinePassed);
        _sent.Count(s => s.Type == NotificationType.TopicDeadlineSoon).Should().Be(2);
        _sent.Should().OnlyContain(s => s.EntityId == null); // Redis guard, not DB dedup, prevents repeats.
        _db.Verify(d => d.DeleteOlderThanAsync(Now - TimeSpan.FromDays(90), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tick_SkipsCandidate_WhenGuardAlreadyHeld()
    {
        SetupCandidates(Candidate("A", Now + TimeSpan.FromHours(24)));
        _guard.Setup(g => g.TryAcquireOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await BuildWorker().Tick(default);

        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_CleanupRunsOnlyOncePerDay()
    {
        SetupCandidates();

        var worker = BuildWorker();
        await worker.Tick(default);
        await worker.Tick(default);

        _db.Verify(d => d.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DeadlineCandidate Candidate(string title, DateTime effectiveDueAt)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), title, effectiveDueAt, Completed: false);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
