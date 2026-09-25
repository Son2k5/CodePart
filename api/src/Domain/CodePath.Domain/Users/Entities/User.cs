using CodePath.Shared.Kernel.Entities;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Domain.Users.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public string? StudentId { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public Guid? ClassId { get; private set; }
    public Class? Class { get; private set; }

    public Guid? FacultyId { get; private set; }
    public Faculty? Faculty { get; private set; }

    public ICollection<Class> ManagedClasses { get; private set; } = new List<Class>();
    public ICollection<Course> TeachingCourses { get; private set; } = new List<Course>();
    public ICollection<Enrollment> Enrollments { get; private set; } = new List<Enrollment>();

    private User() { }

    public static User CreateStudent(
        string fullName,
        string email,
        string passwordHash,
        string studentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        var trimmedFullName = fullName.Trim();
        var trimmedStudentId = studentId.Trim();

        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));
        if (trimmedStudentId.Length > 50)
            throw new ArgumentException("StudentId không được vượt quá 50 ký tự.", nameof(studentId));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Student,
            Status = UserStatus.Active,
            StudentId = trimmedStudentId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateTeacher(
        string fullName,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Teacher,
            Status = UserStatus.Pending,
            StudentId = null,
            CreatedAt = DateTime.UtcNow
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

        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = trimmedFullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            StudentId = null,
            EmailVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void VerifyEmail()
    {
        EmailVerifiedAt = DateTime.UtcNow;
        Touch();
    }

    public void RecordLoginSuccess()
    {
        LastLoginAt = DateTime.UtcNow;
        Touch();
    }

    public void UpdateStatus(UserStatus newStatus)
    {
        Status = newStatus;
        Touch();
    }

    public void UpdateUnverifiedAccount(string fullName, string passwordHash, UserRole role, string? studentId)
    {
        if (EmailVerifiedAt.HasValue)
            throw new InvalidOperationException("Không thể ghi đè tài khoản đã xác thực.");

        var trimmedFullName = fullName.Trim();
        var trimmedStudentId = studentId?.Trim();

        if (trimmedFullName.Length > 200)
            throw new ArgumentException("FullName không được vượt quá 200 ký tự.", nameof(fullName));
        if (trimmedStudentId is not null && trimmedStudentId.Length > 50)
            throw new ArgumentException("StudentId không được vượt quá 50 ký tự.", nameof(studentId));

        FullName = trimmedFullName;
        PasswordHash = passwordHash;
        Role = role;
        StudentId = trimmedStudentId;
        Touch();
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

