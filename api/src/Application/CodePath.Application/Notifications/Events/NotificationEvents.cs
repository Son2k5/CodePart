using CodePath.Domain.Notifications.Enums;
using MediatR;

namespace CodePath.Application.Notifications.Events;

/// <summary>
/// Marker base for cross-module integration events that the Notification module consumes.
/// Producers publish these through <c>IEventBus</c> (which persists them to the durable
/// outbox in the same transaction as the business change); the outbox worker later re-publishes
/// them through MediatR so the matching <see cref="MediatR.INotificationHandler{T}"/> runs.
/// Implements <see cref="INotification"/> purely so it can flow through the existing MediatR
/// pipeline, matching how <c>SubmissionJudgedEvent</c> is dispatched today.
/// </summary>
public abstract record IntegrationEvent : INotification
{
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

// ---- Student-facing events ----

/// <summary>A submission was graded; notify the student with the score.</summary>
public sealed record SubmissionGradedNotification(
    Guid StudentId, Guid SubmissionId, decimal Score, Guid? TopicId, Guid? CourseId) : IntegrationEvent;

/// <summary>An AI hint finished generating; notify the student.</summary>
public sealed record AiHintReadyNotification(
    Guid StudentId, Guid SubmissionId) : IntegrationEvent;

/// <summary>A new topic was published to a class; notify every enrolled student.</summary>
public sealed record NewTopicPublishedNotification(
    Guid ClassId, Guid TopicId, string Title) : IntegrationEvent;

/// <summary>A teacher/admin posted an announcement; notify every enrolled student.</summary>
public sealed record ClassAnnouncementPublishedNotification(
    Guid ClassId, Guid AnnouncementId, string Title) : IntegrationEvent;

/// <summary>A student was removed from a course.</summary>
public sealed record RemovedFromCourseNotification(
    Guid StudentId, Guid CourseId) : IntegrationEvent;

/// <summary>A student's aggregated score in a course changed.</summary>
public sealed record ScoreUpdatedNotification(
    Guid StudentId, Guid CourseId) : IntegrationEvent;

// ---- Teacher-facing events ----

/// <summary>A student submitted work; notify the course teacher (batched downstream).</summary>
public sealed record StudentSubmittedNotification(
    Guid TeacherId, Guid StudentId, Guid SubmissionId, Guid? CourseId) : IntegrationEvent;

/// <summary>A student enrolled in a class; notify the class teachers (resolved downstream).</summary>
public sealed record StudentJoinedCourseNotification(
    Guid ClassId, Guid StudentId) : IntegrationEvent;

/// <summary>An admin approved a pending teacher account.</summary>
public sealed record TeacherAccountApprovedNotification(Guid TeacherId) : IntegrationEvent;

/// <summary>An admin rejected/disabled a pending teacher account.</summary>
public sealed record TeacherAccountRejectedNotification(Guid TeacherId) : IntegrationEvent;

// ---- Admin-facing events ----

/// <summary>A teacher self-registered and is awaiting approval; notify admins.</summary>
public sealed record NewTeacherPendingApprovalNotification(Guid TeacherId) : IntegrationEvent;

// ---- Shared events ----

/// <summary>A new chat message arrived in a conversation.</summary>
public sealed record NewMessageNotification(
    Guid RecipientId, Guid ConversationId, string SenderName) : IntegrationEvent;

// ---- Internal channel dispatch ----

/// <summary>
/// Internal, module-owned event that routes a notification to the email channel through the
/// durable outbox. The notification service stages this (instead of sending SMTP inline) so
/// delivery inherits the worker's at-least-once retry, exponential backoff and dead-lettering.
/// The subject and body are already sanitized plain text.
/// </summary>
public sealed record EmailDispatchNotification(
    Guid RecipientId, NotificationType Type, string Subject, string Body) : IntegrationEvent;
