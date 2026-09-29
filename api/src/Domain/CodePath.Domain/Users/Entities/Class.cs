using CodePath.Domain.Common.Exceptions;
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
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Tên lớp không được để trống.", nameof(name));
        if (string.IsNullOrWhiteSpace(academicYear))
            throw new DomainValidationException("Niên khóa không được để trống.", nameof(academicYear));
        if (facultyId == Guid.Empty)
            throw new DomainValidationException("Faculty ID cannot be empty.", nameof(facultyId));
        if (teacherId == Guid.Empty)
            throw new DomainValidationException("Teacher ID cannot be empty.", nameof(teacherId));

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
            throw new DomainValidationException("Teacher ID cannot be empty.", nameof(teacherId));

        TeacherId = teacherId;
        Touch();
    }

    public void UpdateDetails(string name, string academicYear)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Tên lớp không được để trống.", nameof(name));
        if (string.IsNullOrWhiteSpace(academicYear))
            throw new DomainValidationException("Niên khóa không được để trống.", nameof(academicYear));

        Name = name.Trim();
        AcademicYear = academicYear.Trim();
        Touch();
    }
}
