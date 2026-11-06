using System.Text.Json.Serialization;

namespace CodePath.Domain.Notifications.Enums;

/// <summary>
/// All notification categories for MVP. Stored as an int column.
/// Recipient routing (who receives which type) is enforced by the publishing module,
/// not by this enum.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter<NotificationType>))]
public enum NotificationType
{
    // Student-facing
    SubmissionGraded = 1,
    AiHintReady = 2,
    TopicDeadlineSoon = 3,
    TopicDeadlinePassed = 4,
    NewTopicPublished = 5,
    RemovedFromCourse = 6,
    ScoreUpdated = 7,
    // Teacher/admin class announcement. Added after MVP release: value 8 keeps historical rows intact.
    ClassAnnouncement = 8,

    // Teacher-facing
    StudentJoinedCourse = 10,
    StudentSubmitted = 11,
    TeacherAccountApproved = 12,
    TeacherAccountRejected = 13,

    // Admin-facing
    NewTeacherPendingApproval = 20,

    // Shared
    NewMessage = 30,
    SystemBroadcast = 31,

    // System / email-only (never persisted in-app)
    AccountVerifiedOtp = 40
}
