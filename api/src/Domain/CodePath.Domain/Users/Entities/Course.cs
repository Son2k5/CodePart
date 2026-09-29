using CodePath.Domain.Common.Exceptions;
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
        if (string.IsNullOrWhiteSpace(subjectName))
            throw new DomainValidationException("Tên môn học không được để trống.", nameof(subjectName));
        if (string.IsNullOrWhiteSpace(sectionCode))
            throw new DomainValidationException("Mã lớp học phần không được để trống.", nameof(sectionCode));
        if (string.IsNullOrWhiteSpace(language))
            throw new DomainValidationException("Ngôn ngữ không được để trống.", nameof(language));
        if (string.IsNullOrWhiteSpace(semester))
            throw new DomainValidationException("Học kỳ không được để trống.", nameof(semester));
        if (string.IsNullOrWhiteSpace(joinCode))
            throw new DomainValidationException("Mã tham gia không được để trống.", nameof(joinCode));
        if (teacherId == Guid.Empty)
            throw new DomainValidationException("Teacher ID cannot be empty.", nameof(teacherId));
        if (maxCapacity <= 0)
            throw new DomainValidationException("Max capacity must be greater than 0.", nameof(maxCapacity));

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
            throw new DomainRuleViolationException("Course is already active.");

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
            throw new DomainValidationException("Teacher ID cannot be empty.", nameof(teacherId));

        TeacherId = teacherId;
        Touch();
    }
}
