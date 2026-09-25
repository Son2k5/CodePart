using CodePath.Shared.Kernel.Entities;
using CodePath.Domain.Users.Enums;

namespace CodePath.Domain.Users.Entities;

public sealed class Course : BaseEntity
{
    public string SubjectName { get; private set; } = default!;
    public string SectionCode { get; private set; } = default!;
    public string Language { get; private set; } = default!;
    public string Semester { get; private set; } = default!;
    public string JoinCode { get; private set; } = default!;
    public int MaxCapacity { get; private set; } = 60;
    public CourseStatus Status { get; private set; } = CourseStatus.Draft;

    public Guid TeacherId { get; private set; }
    public User Teacher { get; private set; } = default!;

    public ICollection<Enrollment> Enrollments { get; private set; } = new List<Enrollment>();

    private Course() { }

    public static Course Create(
        string subjectName,
        string sectionCode,
        string language,
        string semester,
        string joinCode,
        Guid teacherId,
        int maxCapacity = 60)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(semester);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        if (teacherId == Guid.Empty) throw new ArgumentException("Teacher ID cannot be empty.", nameof(teacherId));
        if (maxCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Max capacity must be greater than 0.");

        return new Course
        {
            Id = Guid.NewGuid(),
            SubjectName = subjectName.Trim(),
            SectionCode = sectionCode.Trim(),
            Language = language.Trim(),
            Semester = semester.Trim(),
            JoinCode = joinCode.Trim().ToUpperInvariant(),
            TeacherId = teacherId,
            MaxCapacity = maxCapacity,
            Status = CourseStatus.Draft
        };
    }

    public void Activate()
    {
        if (Status == CourseStatus.Active)
            throw new InvalidOperationException("Course is already active.");

        Status = CourseStatus.Active;
        Touch();
    }

    public void Close()
    {
        Status = CourseStatus.Closed;
        Touch();
    }

    public void AssignTeacher(Guid teacherId)
    {
        if (teacherId == Guid.Empty)
            throw new ArgumentException("Teacher ID cannot be empty.", nameof(teacherId));

        TeacherId = teacherId;
        Touch();
    }
}
