using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Notifications.Contracts;
using CodePath.Application.Notifications.Events;
using CodePath.Domain.Notifications.Enums;
using CodePath.Shared.Kernel.Enums;
using MediatR;

namespace CodePath.Application.Notifications.Handlers;

// Each handler translates an integration event (re-published by the outbox worker) into a
// delivery through INotificationService, which owns preference gating, dedup and persistence.
// Handlers stay thin: no channel logic here, only "who gets what copy".

public sealed class SubmissionGradedHandler(INotificationService notifications)
    : INotificationHandler<SubmissionGradedNotification>
{
    public Task Handle(SubmissionGradedNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.StudentId, NotificationType.SubmissionGraded,
            "Your submission has been graded",
            $"You scored {e.Score:0.##} on a submission.",
            EntityId: e.SubmissionId,
            Payload: new Dictionary<string, object?>
            {
                ["submissionId"] = e.SubmissionId,
                ["score"] = e.Score,
                ["topicId"] = e.TopicId,
                ["courseId"] = e.CourseId,
            }), ct);
}

public sealed class AiHintReadyHandler(INotificationService notifications)
    : INotificationHandler<AiHintReadyNotification>
{
    public Task Handle(AiHintReadyNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.StudentId, NotificationType.AiHintReady,
            "A new hint is ready",
            "An AI hint for your submission is ready to view.",
            EntityId: e.SubmissionId,
            Payload: new Dictionary<string, object?> { ["submissionId"] = e.SubmissionId }), ct);
}

public sealed class RemovedFromCourseHandler(INotificationService notifications)
    : INotificationHandler<RemovedFromCourseNotification>
{
    public Task Handle(RemovedFromCourseNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.StudentId, NotificationType.RemovedFromCourse,
            "You were removed from a course",
            "You no longer have access to this course.",
            EntityId: e.CourseId,
            Payload: new Dictionary<string, object?> { ["courseId"] = e.CourseId }), ct);
}

public sealed class ScoreUpdatedHandler(INotificationService notifications)
    : INotificationHandler<ScoreUpdatedNotification>
{
    public Task Handle(ScoreUpdatedNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.StudentId, NotificationType.ScoreUpdated,
            "Your course score changed",
            "Your aggregated score in a course has been updated.",
            EntityId: e.CourseId,
            Payload: new Dictionary<string, object?> { ["courseId"] = e.CourseId }), ct);
}

public sealed class StudentSubmittedHandler(INotificationService notifications)
    : INotificationHandler<StudentSubmittedNotification>
{
    public Task Handle(StudentSubmittedNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.TeacherId, NotificationType.StudentSubmitted,
            "New submission received",
            "A student submitted work that is awaiting review.",
            EntityId: e.SubmissionId,
            Payload: new Dictionary<string, object?>
            {
                ["submissionId"] = e.SubmissionId,
                ["studentId"] = e.StudentId,
                ["courseId"] = e.CourseId,
            }), ct);
}

public sealed class StudentJoinedCourseHandler(
    INotificationService notifications, INotificationDbContext db)
    : INotificationHandler<StudentJoinedCourseNotification>
{
    public async Task Handle(StudentJoinedCourseNotification e, CancellationToken ct)
    {
        var teachers = await db.GetClassTeacherIdsAsync(e.ClassId, ct);
        if (teachers.Count == 0) return;

        await notifications.SendBulkAsync(new BulkNotificationRequest(
            teachers, NotificationType.StudentJoinedCourse,
            "A student joined your class",
            "A new student enrolled in one of your classes.",
            EntityId: e.ClassId,
            Payload: new Dictionary<string, object?>
            {
                ["studentId"] = e.StudentId,
                ["classId"] = e.ClassId,
            }), ct);
    }
}

public sealed class TeacherAccountApprovedHandler(INotificationService notifications)
    : INotificationHandler<TeacherAccountApprovedNotification>
{
    public Task Handle(TeacherAccountApprovedNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.TeacherId, NotificationType.TeacherAccountApproved,
            "Your teacher account is approved",
            "Your account has been approved. You can now create and manage courses.",
            Payload: null), ct);
}

public sealed class TeacherAccountRejectedHandler(INotificationService notifications)
    : INotificationHandler<TeacherAccountRejectedNotification>
{
    public Task Handle(TeacherAccountRejectedNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.TeacherId, NotificationType.TeacherAccountRejected,
            "Your teacher account was not approved",
            "Your account application was rejected or suspended. Please contact an administrator.",
            Payload: null), ct);
}

public sealed class NewMessageHandler(INotificationService notifications)
    : INotificationHandler<NewMessageNotification>
{
    public Task Handle(NewMessageNotification e, CancellationToken ct) =>
        notifications.SendAsync(new NotificationRequest(
            e.RecipientId, NotificationType.NewMessage,
            "New message",
            $"{e.SenderName} sent you a message.",
            EntityId: e.ConversationId,
            Payload: new Dictionary<string, object?>
            {
                ["conversationId"] = e.ConversationId,
                ["senderName"] = e.SenderName,
            }), ct);
}

// Fan-out handlers resolve the recipient set then send in bulk.

public sealed class NewTopicPublishedHandler(
    INotificationService notifications, INotificationDbContext db)
    : INotificationHandler<NewTopicPublishedNotification>
{
    public async Task Handle(NewTopicPublishedNotification e, CancellationToken ct)
    {
        var students = await db.GetClassStudentIdsAsync(e.ClassId, ct);
        if (students.Count == 0) return;

        await notifications.SendBulkAsync(new BulkNotificationRequest(
            students, NotificationType.NewTopicPublished,
            "New lesson published",
            $"A new topic \"{e.Title}\" is available in your class.",
            EntityId: e.TopicId,
            Payload: new Dictionary<string, object?>
            {
                ["topicId"] = e.TopicId,
                ["classId"] = e.ClassId,
            }), ct);
    }
}

public sealed class ClassAnnouncementPublishedHandler(
    INotificationService notifications, INotificationDbContext db)
    : INotificationHandler<ClassAnnouncementPublishedNotification>
{
    public async Task Handle(ClassAnnouncementPublishedNotification e, CancellationToken ct)
    {
        var students = await db.GetClassStudentIdsAsync(e.ClassId, ct);
        if (students.Count == 0) return;

        await notifications.SendBulkAsync(new BulkNotificationRequest(
            students, NotificationType.ClassAnnouncement,
            e.Title,
            "Your class teacher posted a new announcement.",
            EntityId: e.AnnouncementId,
            Payload: new Dictionary<string, object?>
            {
                ["announcementId"] = e.AnnouncementId,
                ["classId"] = e.ClassId,
            }), ct);
    }
}

public sealed class NewTeacherPendingApprovalHandler(
    INotificationService notifications, INotificationDbContext db)
    : INotificationHandler<NewTeacherPendingApprovalNotification>
{
    public async Task Handle(NewTeacherPendingApprovalNotification e, CancellationToken ct)
    {
        var admins = await db.GetUserIdsByRoleAsync(UserRole.Admin, ct);
        if (admins.Count == 0) return;

        await notifications.SendBulkAsync(new BulkNotificationRequest(
            admins, NotificationType.NewTeacherPendingApproval,
            "New teacher awaiting approval",
            "A teacher account has been created and is pending approval.",
            EntityId: e.TeacherId,
            Payload: new Dictionary<string, object?> { ["teacherId"] = e.TeacherId }), ct);
    }
}
