using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Application.Notifications.Events;
using CodePath.Application.Notifications.Validation;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Infrastructure.Notifications;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private readonly Mock<INotificationDbContext> _db = new();
    private readonly Mock<IRealtimeNotifier> _realtime = new();
    private readonly Mock<IUnreadCountCache> _cache = new();
    private readonly Mock<IEventBus> _events = new();
    private readonly List<Notification> _added = new();

    public NotificationServiceTests()
    {
        _db.Setup(d => d.AddNotificationAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => _added.Add(n));
        _db.Setup(d => d.AddNotificationsAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((ns, _) => _added.AddRange(ns));
        _db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _db.Setup(d => d.DedupExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _events.Setup(e => e.PublishAsync(It.IsAny<EmailDispatchNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private NotificationService CreateService() => new(
        _db.Object,
        new NotificationRequestValidator(),
        new BulkNotificationRequestValidator(),
        new BroadcastRequestValidator(),
        new NotificationPreferenceUpdateValidator(),
        TimeProvider.System,
        NullLogger<NotificationService>.Instance,
        _realtime.Object,
        _cache.Object,
        _events.Object);

    [Fact]
    public async Task Send_PersistsWhenInAppEnabled_AndReturnsDto()
    {
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null); // default: enabled

        var recipient = Guid.NewGuid();
        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.SubmissionGraded, "Graded", "You scored 9/10"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(NotificationType.SubmissionGraded);
        _added.Should().ContainSingle();
        _db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_SanitizesHtmlInTitleAndBody()
    {
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        var result = await CreateService().SendAsync(new NotificationRequest(
            Guid.NewGuid(), NotificationType.NewTopicPublished,
            "<b>New topic</b>", "<script>alert(1)</script>Check it out"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().NotContain("<b>");
        result.Value.Body.Should().NotContain("<script");
    }

    [Fact]
    public async Task Send_WhenInAppDisabledByPreference_SkipsPersistence()
    {
        var recipient = Guid.NewGuid();
        _db.Setup(d => d.GetPreferenceAsync(recipient, NotificationType.AiHintReady, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationPreference.Create(recipient, NotificationType.AiHintReady, inAppEnabled: false, emailEnabled: true, DateTime.UtcNow));

        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.AiHintReady, "Hint", "ready"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_WhenDedupKeyAlreadyExists_IsIdempotentNoOp()
    {
        var recipient = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);
        _db.Setup(d => d.DedupExistsAsync(recipient, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.SubmissionGraded, "Graded", "body", entityId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task SendBulk_FiltersRecipientsWithInAppDisabled_AndCountsPersisted()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, NotificationType _, CancellationToken _) =>
                userId == b
                    ? NotificationPreference.Create(b, NotificationType.NewTopicPublished, inAppEnabled: false, emailEnabled: true, DateTime.UtcNow)
                    : null);

        var result = await CreateService().SendBulkAsync(new BulkNotificationRequest(
            [a, b, c], NotificationType.NewTopicPublished, "New topic", "open"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _added.Should().HaveCount(2).And.NotContain(n => n.RecipientId == b);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotOwnedByRecipient_ReturnsNotFound()
    {
        _db.Setup(d => d.GetForRecipientAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var result = await CreateService().MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task List_DetectsFurtherPageAndEmitsCursor()
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, NotificationLimits.DefaultPageSize + 1)
            .Select(i => Notification.Create(Guid.NewGuid(), NotificationType.SubmissionGraded, "t", "b", null, null, now.AddMinutes(-i)))
            .ToList();
        _db.Setup(d => d.QueryAsync(It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<NotificationType?>(),
                It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var page = await CreateService().ListAsync(Guid.NewGuid(), null, null, null, 0);

        page.HasMore.Should().BeTrue();
        page.Items.Should().HaveCount(NotificationLimits.DefaultPageSize);
        page.NextCursor.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Send_RejectsOversizedTitle()
    {
        var result = await CreateService().SendAsync(new NotificationRequest(
            Guid.NewGuid(), NotificationType.NewMessage, new string('x', NotificationLimits.MaxTitleLength + 1), "b"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Send_PushesRealtime_AndBumpsUnreadCache()
    {
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);
        var recipient = Guid.NewGuid();

        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.AiHintReady, "Hint", "ready"));

        result.IsSuccess.Should().BeTrue();
        _realtime.Verify(r => r.PushAsync(recipient, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.IncrementAsync(recipient, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCachedValue_WithoutTouchingDatabase()
    {
        var recipient = Guid.NewGuid();
        _cache.Setup(c => c.GetAsync(recipient, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var count = await CreateService().GetUnreadCountAsync(recipient);

        count.Should().Be(7);
        _db.Verify(d => d.CountUnreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsRead_InvalidatesUnreadCache()
    {
        var recipient = Guid.NewGuid();
        var notification = Notification.Create(recipient, NotificationType.NewMessage, "t", "b", null, null, DateTime.UtcNow);
        _db.Setup(d => d.GetForRecipientAsync(notification.Id, recipient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var result = await CreateService().MarkAsReadAsync(recipient, notification.Id);

        result.IsSuccess.Should().BeTrue();
        _cache.Verify(c => c.InvalidateAsync(recipient, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_EnqueuesEmailDispatch_WhenEmailEnabled()
    {
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null); // default: email on
        var recipient = Guid.NewGuid();

        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.SubmissionGraded, "Graded", "You scored 9/10"));

        result.IsSuccess.Should().BeTrue();
        _events.Verify(e => e.PublishAsync(
            It.Is<EmailDispatchNotification>(m => m.RecipientId == recipient && m.Type == NotificationType.SubmissionGraded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_DoesNotEnqueueEmail_WhenAllowEmailFalse()
    {
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        var result = await CreateService().SendAsync(new NotificationRequest(
            Guid.NewGuid(), NotificationType.AiHintReady, "Hint", "ready", AllowEmail: false));

        result.IsSuccess.Should().BeTrue();
        _events.Verify(e => e.PublishAsync(
            It.IsAny<EmailDispatchNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Send_AccountVerifiedOtp_EmailsOnly_WithoutPersistingInApp()
    {
        var recipient = Guid.NewGuid();

        var result = await CreateService().SendAsync(
            new NotificationRequest(recipient, NotificationType.AccountVerifiedOtp, "Verify", "Your code is 123456"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _added.Should().BeEmpty();
        _events.Verify(e => e.PublishAsync(
            It.Is<EmailDispatchNotification>(m => m.RecipientId == recipient && m.Type == NotificationType.AccountVerifiedOtp),
            It.IsAny<CancellationToken>()), Times.Once);
        _db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Broadcast_PersistsRecipients_AndWritesSingleAuditLog()
    {
        var actor = Guid.NewGuid();
        var recipients = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _db.Setup(d => d.GetAllActiveUserIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(recipients);
        _db.Setup(d => d.GetPreferenceAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        AuditLog? audit = null;
        _db.Setup(d => d.AddAuditLogAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => audit = a);

        var result = await CreateService().BroadcastAsync(
            new BroadcastRequest("Maintenance", "Back at 22:00", BroadcastScope.AllUsers), actor, "203.0.113.7");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        audit.Should().NotBeNull();
        audit!.ActorId.Should().Be(actor);
        audit.Action.Should().Be(AuditAction.NotificationBroadcast);
        audit.IpAddress.Should().Be("203.0.113.7");
        _db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
