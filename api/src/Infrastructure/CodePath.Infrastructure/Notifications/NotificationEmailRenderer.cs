using System.Net;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Domain.Notifications.Enums;

namespace CodePath.Infrastructure.Notifications;

/// <summary>
/// Builds a self-contained, inline-styled HTML email for a notification. Deliberately dependency
/// free (no Razor/Liquid engine) to honour the project's "minimal new libraries" constraint: the
/// body is plain, already-sanitized text that we HTML-encode here, so there is no injection risk,
/// and only the fixed layout/label changes per notification type.
/// </summary>
public sealed class NotificationEmailRenderer : INotificationEmailRenderer
{
    public string Render(NotificationType type, string subject, string body)
    {
        var label = LabelFor(type);
        var safeSubject = WebUtility.HtmlEncode(subject);
        var safeBody = WebUtility.HtmlEncode(body).Replace("\n", "<br />");

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <body style="margin:0;padding:0;background-color:#f4f5f7;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2937;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.08);">
                      <tr>
                        <td style="background:#2563eb;padding:20px 24px;">
                          <span style="color:#ffffff;font-size:13px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;">CodePath &middot; {label}</span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px 24px;">
                          <h1 style="margin:0 0 12px;font-size:20px;line-height:1.3;color:#111827;">{safeSubject}</h1>
                          <p style="margin:0;font-size:15px;line-height:1.6;color:#374151;">{safeBody}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 24px;background:#f9fafb;border-top:1px solid #e5e7eb;">
                          <p style="margin:0;font-size:12px;line-height:1.5;color:#9ca3af;">
                            You are receiving this email because notifications are enabled on your CodePath account.
                            You can adjust this in your notification settings.
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string LabelFor(NotificationType type) => type switch
    {
        NotificationType.SubmissionGraded => "Submission graded",
        NotificationType.AiHintReady => "AI hint ready",
        NotificationType.TopicDeadlineSoon => "Deadline approaching",
        NotificationType.TopicDeadlinePassed => "Deadline passed",
        NotificationType.NewTopicPublished => "New lesson",
        NotificationType.ClassAnnouncement => "Class announcement",
        NotificationType.RemovedFromCourse => "Course access",
        NotificationType.ScoreUpdated => "Score update",
        NotificationType.StudentJoinedCourse => "New student",
        NotificationType.StudentSubmitted => "New submission",
        NotificationType.TeacherAccountApproved => "Account approved",
        NotificationType.TeacherAccountRejected => "Account review",
        NotificationType.NewTeacherPendingApproval => "Approval request",
        NotificationType.NewMessage => "Message",
        NotificationType.SystemBroadcast => "Announcement",
        NotificationType.AccountVerifiedOtp => "Verification",
        _ => "Notification",
    };
}
