using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Domain.Users.Entities;

public sealed partial class User
{
    public DateOnly? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? AcademicTitle { get; private set; }
    public int Version { get; private set; } = 1;

    public void UpdateProfile(string fullName, string? phoneNumber, DateOnly? dateOfBirth,
        string? gender, string? avatarUrl, Guid? administrativeClassId, Guid? facultyId,
        string? academicTitle, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length is < 2 or > 100)
            throw new DomainValidationException("Full name must contain 2 to 100 characters.");
        if (dateOfBirth > DateOnly.FromDateTime(utcNow))
            throw new DomainValidationException("Date of birth cannot be in the future.");
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        AvatarUrl = avatarUrl;
        if (Role == UserRole.Student) ClassId = administrativeClassId;
        if (Role == UserRole.Teacher) { FacultyId = facultyId; AcademicTitle = academicTitle; }
        Touch(utcNow);
    }

    public void ChangePasswordHash(string hash, DateTime utcNow)
    {
        ChangePassword(hash, utcNow);
        IncrementSecurityVersion(utcNow);
    }

    public void UpdateAvatar(string url, DateTime utcNow) { AvatarUrl = url; Touch(utcNow); }

    public void UpdateAccess(UserRole role, UserStatus status, DateTime utcNow)
    {
        if (!Enum.IsDefined(role) || !Enum.IsDefined(status))
            throw new DomainValidationException("Invalid role or status.");
        if (role == UserRole.Student && string.IsNullOrWhiteSpace(StudentId))
            throw new DomainValidationException("A student code is required before assigning the Student role.");
        if (Role != role || Status != status) IncrementSecurityVersion(utcNow);
        Role = role;
        Status = status;
        if (role != UserRole.Student) ClassId = null;
        if (role != UserRole.Teacher) AcademicTitle = null;
        Touch(utcNow);
    }
}
