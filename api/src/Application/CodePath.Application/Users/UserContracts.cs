using System.Text.Json.Serialization;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Auditing.Entities;
using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Users;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateProfileRequest(string FullName, string? PhoneNumber, DateOnly? DateOfBirth,
    string? Gender, string? AvatarUrl, Guid? AdministrativeClassId, Guid? FacultyId,
    string? AcademicTitle, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateUserRequest(string FullName, string? PhoneNumber, DateOnly? DateOfBirth,
    string? Gender, string? AvatarUrl, Guid? AdministrativeClassId, Guid? FacultyId,
    string? AcademicTitle, int Version, UserRole Role, UserStatus Status)
    : UpdateProfileRequest(FullName, PhoneNumber, DateOfBirth, Gender, AvatarUrl, AdministrativeClassId, FacultyId, AcademicTitle, Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmPassword);
public sealed record UserProfileResponse(Guid Id, string Email, string? StudentCode, string FullName,
    string? PhoneNumber, DateOnly? DateOfBirth, string? Gender, string? AvatarUrl,
    Guid? AdministrativeClassId, Guid? FacultyId, string? AcademicTitle, UserRole Role, UserStatus Status, int Version)
{
    public static UserProfileResponse From(User u) => new(u.Id, u.Email, u.StudentId, u.FullName,
        u.PhoneNumber, u.DateOfBirth, u.Gender, u.AvatarUrl, u.ClassId, u.FacultyId, u.AcademicTitle, u.Role, u.Status, u.Version);
}

/// <summary>Tuỳ chọn cho select lớp hành chính / khoa trong biểu hồ sơ.</summary>
public sealed record OptionItem(Guid Id, string Name);
public sealed record ProfileOptionsResponse(IReadOnlyList<OptionItem> AdministrativeClasses, IReadOnlyList<OptionItem> Faculties);

public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ClassExistsAsync(Guid id, CancellationToken ct);
    Task<bool> FacultyExistsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OptionItem>> GetAdministrativeClassOptionsAsync(CancellationToken ct);
    Task<IReadOnlyList<OptionItem>> GetFacultyOptionsAsync(CancellationToken ct);
    Task SaveAsync(User user, int expectedVersion, AuditLog? audit, bool revokeSessions, CancellationToken ct);
}
public interface IAvatarStorage
{
    Task<string> UploadAsync(Guid userId, byte[] content, string extension, CancellationToken ct);
}
public interface IUserService
{
    Task<UserProfileResponse> GetMyProfile(Guid userId, CancellationToken ct);
    Task<UserProfileResponse> UpdateMyProfile(Guid userId, UpdateProfileRequest request, CancellationToken ct);
    Task ChangePassword(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task<UserProfileResponse> UploadAvatar(Guid userId, byte[] content, string fileName, int version, CancellationToken ct);
    Task<UserProfileResponse> AdminUpdateUser(Guid actorId, Guid userId, AdminUpdateUserRequest request, CancellationToken ct);
    Task<ProfileOptionsResponse> GetProfileOptions(CancellationToken ct);
    Task<UserProfileResponse> AdminGetUser(Guid targetId, CancellationToken ct);
}
