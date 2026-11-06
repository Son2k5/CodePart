using CodePath.Shared.Kernel.Entities;
namespace CodePath.Domain.Learning;

/// <summary>
/// A class-scoped announcement written by a teacher (or an admin acting for a class without
/// a teacher). Students enrolled in the class see it on the class detail page and receive an
/// in-app notification through the notification fan-out.
/// </summary>
public sealed class ClassAnnouncement : BaseEntity
{
    public Guid ClassId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }

    private ClassAnnouncement() { }

    public static ClassAnnouncement Create(Guid classId, Guid senderId, string title, string body, DateTime utcNow)
    {
        if (classId == Guid.Empty) throw new ArgumentException("Class id cannot be empty.", nameof(classId));
        if (senderId == Guid.Empty) throw new ArgumentException("Sender id cannot be empty.", nameof(senderId));

        return new ClassAnnouncement
        {
            ClassId = classId,
            SenderId = senderId,
            Title = title,
            Body = body,
            CreatedAt = utcNow
        };
    }

    public void Delete(DateTime utcNow)
    {
        IsDeleted = true;
        Touch(utcNow);
    }
}
