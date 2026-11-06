using System.Text.Json;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Application.Notifications.Events;
using CodePath.Application.Notifications.Handlers;
using CodePath.Domain.Notifications.Entities;
using CodePath.Domain.Notifications.Enums;
using CodePath.Infrastructure.Notifications;
using CodePath.Shared.Kernel.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Notifications;

public sealed class NotificationOutboxTests
{
    private readonly Mock<INotificationDbContext> _db = new();
    private readonly List<OutboxMessage> _staged = new();

    public NotificationOutboxTests()
    {
        _db.Setup(d => d.AddOutboxAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => _staged.Add(m));
    }

    [Fact]
    public async Task PublishAsync_StagesOutboxRow_WithoutSaving()
    {
        var bus = new OutboxEventBus(_db.Object, TimeProvider.System);
        var student = Guid.NewGuid();
        var submission = Guid.NewGuid();

        await bus.PublishAsync(new SubmissionGradedNotification(student, submission, 9.5m, null, null));

        _staged.Should().ContainSingle();
        var msg = _staged[0];
        msg.EventType.Should().Be(typeof(SubmissionGradedNotification).AssemblyQualifiedName);
        msg.ProcessedAtUtc.Should().BeNull();
        msg.DeadLetteredAtUtc.Should().BeNull();
        _db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_PayloadRoundTripsToSameEvent()
    {
        var bus = new OutboxEventBus(_db.Object, TimeProvider.System);
        var student = Guid.NewGuid();
        var submission = Guid.NewGuid();
        var original = new SubmissionGradedNotification(student, submission, 9.5m, Guid.NewGuid(), null);

        await bus.PublishAsync(original);

        var msg = _staged.Single();
        var type = Type.GetType(msg.EventType);
        type.Should().NotBeNull("worker resolves the CLR type from the stored assembly-qualified name");

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var restored = (SubmissionGradedNotification?)JsonSerializer.Deserialize(msg.Payload, type!, options);

        restored.Should().Be(original);
    }

    [Fact]
    public async Task SubmissionGradedHandler_MapsEventToSendRequest()
    {
        var service = new Mock<INotificationService>();
        NotificationRequest? captured = null;
        service.Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<NotificationDto?>.Success(null));

        var student = Guid.NewGuid();
        var submission = Guid.NewGuid();
        await new SubmissionGradedHandler(service.Object)
            .Handle(new SubmissionGradedNotification(student, submission, 8m, null, null), default);

        captured.Should().NotBeNull();
        captured!.RecipientId.Should().Be(student);
        captured.Type.Should().Be(NotificationType.SubmissionGraded);
        captured.EntityId.Should().Be(submission);
    }

    [Fact]
    public async Task NewTopicPublishedHandler_FansOutToClassStudents()
    {
        var service = new Mock<INotificationService>();
        var students = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var classId = Guid.NewGuid();
        _db.Setup(d => d.GetClassStudentIdsAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(students);

        BulkNotificationRequest? bulk = null;
        service.Setup(s => s.SendBulkAsync(It.IsAny<BulkNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BulkNotificationRequest, CancellationToken>((r, _) => bulk = r)
            .ReturnsAsync(Result<int>.Success(students.Count));

        await new NewTopicPublishedHandler(service.Object, _db.Object)
            .Handle(new NewTopicPublishedNotification(classId, Guid.NewGuid(), "Intro to Loops"), default);

        bulk.Should().NotBeNull();
        bulk!.RecipientIds.Should().BeSameAs(students);
        bulk.Type.Should().Be(NotificationType.NewTopicPublished);
    }

    [Fact]
    public async Task NewTopicPublishedHandler_WhenNoStudents_DoesNotSend()
    {
        var service = new Mock<INotificationService>();
        _db.Setup(d => d.GetClassStudentIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        await new NewTopicPublishedHandler(service.Object, _db.Object)
            .Handle(new NewTopicPublishedNotification(Guid.NewGuid(), Guid.NewGuid(), "Empty"), default);

        service.Verify(s => s.SendBulkAsync(It.IsAny<BulkNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StudentJoinedCourseHandler_FansOutToClassTeachers()
    {
        var service = new Mock<INotificationService>();
        var teachers = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var classId = Guid.NewGuid();
        _db.Setup(d => d.GetClassTeacherIdsAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(teachers);

        BulkNotificationRequest? bulk = null;
        service.Setup(s => s.SendBulkAsync(It.IsAny<BulkNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BulkNotificationRequest, CancellationToken>((r, _) => bulk = r)
            .ReturnsAsync(Result<int>.Success(teachers.Count));

        await new StudentJoinedCourseHandler(service.Object, _db.Object)
            .Handle(new StudentJoinedCourseNotification(classId, Guid.NewGuid()), default);

        bulk.Should().NotBeNull();
        bulk!.RecipientIds.Should().BeSameAs(teachers);
        bulk.Type.Should().Be(NotificationType.StudentJoinedCourse);
    }

    [Fact]
    public async Task TeacherAccountApprovedHandler_NotifiesTeacher()
    {
        var service = new Mock<INotificationService>();
        NotificationRequest? captured = null;
        service.Setup(s => s.SendAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<NotificationDto?>.Success(null));

        var teacher = Guid.NewGuid();
        await new TeacherAccountApprovedHandler(service.Object)
            .Handle(new TeacherAccountApprovedNotification(teacher), default);

        captured!.RecipientId.Should().Be(teacher);
        captured.Type.Should().Be(NotificationType.TeacherAccountApproved);
    }
}
