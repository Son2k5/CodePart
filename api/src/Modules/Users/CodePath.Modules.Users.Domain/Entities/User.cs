using CodePath.Domain.Common;
using CodePath.Modules.Users.Domain.Enums;

namespace CodePath.Modules.Users.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public int FailedLoginCount { get; private set; } = 0;
    public DateTime? LockoutEnd { get; private set; }

    // Student specific attributes
    public string? StudentCode { get; private set; }
    public Guid? ClassId { get; private set; }
    public Class? Class { get; private set; }

    // Teacher specific attributes (Teacher belongs directly to Faculty)
    public Guid? FacultyId { get; private set; }
    public Faculty? Faculty { get; private set; }

    // Teacher navigation collections
    public ICollection<Class> ManagedClasses { get; private set; } = new List<Class>();
    public ICollection<Course> TeachingCourses { get; private set; } = new List<Course>();

    // Student navigation collections
    public ICollection<Enrollment> Enrollments { get; private set; } = new List<Enrollment>();

    private User() { }

    public static User CreateStudent(
        string fullName,
        string email,
        string passwordHash,
        string studentCode,
        Guid classId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentCode);
        if (classId == Guid.Empty) throw new ArgumentException("Class ID cannot be empty.", nameof(classId));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Student,
            StudentCode = studentCode.Trim().ToUpperInvariant(),
            ClassId = classId,
            IsActive = true
        };
    }

    public static User CreateTeacher(
        string fullName,
        string email,
        string passwordHash,
        Guid facultyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (facultyId == Guid.Empty) throw new ArgumentException("Faculty ID cannot be empty.", nameof(facultyId));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Teacher,
            FacultyId = facultyId,
            IsActive = true
        };
    }

    public static User CreateAdmin(
        string fullName,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsActive = true
        };
    }

    public void AssignToClass(Guid classId)
    {
        if (Role != UserRole.Student)
            throw new InvalidOperationException("Only students can be assigned to a class.");
        if (classId == Guid.Empty)
            throw new ArgumentException("Class ID cannot be empty.", nameof(classId));

        ClassId = classId;
        Touch();
    }

    public void AssignToFaculty(Guid facultyId)
    {
        if (Role != UserRole.Teacher)
            throw new InvalidOperationException("Only teachers can be assigned directly to a faculty.");
        if (facultyId == Guid.Empty)
            throw new ArgumentException("Faculty ID cannot be empty.", nameof(facultyId));

        FacultyId = facultyId;
        Touch();
    }

    public void RecordLoginSuccess()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
        LastLoginAt = DateTime.UtcNow;
        Touch();
    }

    public void RecordLoginFailure(int maxAttempts = 5, TimeSpan? lockoutDuration = null)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEnd = DateTime.UtcNow.Add(lockoutDuration ?? TimeSpan.FromMinutes(15));
        }
        Touch();
    }

    public void VerifyEmail()
    {
        EmailVerifiedAt = DateTime.UtcNow;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void UpdateProfile(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        Touch();
    }
}
