using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Users.Entities;

public sealed class Class : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string AcademicYear { get; private set; } = default!;

    public Guid FacultyId { get; private set; }
    public Faculty Faculty { get; private set; } = default!;

    public Guid TeacherId { get; private set; }
    public User Teacher { get; private set; } = default!;

    public ICollection<User> Students { get; private set; } = new List<User>();

    private Class() { }

    public static Class Create(string name, string academicYear, Guid facultyId, Guid teacherId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(academicYear);
        if (facultyId == Guid.Empty) throw new ArgumentException("Faculty ID cannot be empty.", nameof(facultyId));
        if (teacherId == Guid.Empty) throw new ArgumentException("Teacher ID cannot be empty.", nameof(teacherId));

        return new Class
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            AcademicYear = academicYear.Trim(),
            FacultyId = facultyId,
            TeacherId = teacherId
        };
    }

    public void AssignTeacher(Guid teacherId)
    {
        if (teacherId == Guid.Empty)
            throw new ArgumentException("Teacher ID cannot be empty.", nameof(teacherId));

        TeacherId = teacherId;
        Touch();
    }

    public void UpdateDetails(string name, string academicYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(academicYear);

        Name = name.Trim();
        AcademicYear = academicYear.Trim();
        Touch();
    }
}
