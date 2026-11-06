using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;

namespace CodePath.Application.Users;

public sealed class UserService(IUserRepository repository, IPasswordHasher hasher,
    IAvatarStorage avatars, TimeProvider clock) : IUserService
{
    private async Task<User> Find(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException("User not found.");
    public async Task<UserProfileResponse> GetMyProfile(Guid userId, CancellationToken ct) => UserProfileResponse.From(await Find(userId, ct));

    public async Task<ProfileOptionsResponse> GetProfileOptions(CancellationToken ct)
    {
        var classes = await repository.GetAdministrativeClassOptionsAsync(ct);
        var faculties = await repository.GetFacultyOptionsAsync(ct);
        return new ProfileOptionsResponse(classes, faculties);
    }

    public async Task<UserProfileResponse> AdminGetUser(Guid targetId, CancellationToken ct) => UserProfileResponse.From(await Find(targetId, ct));

    public async Task<UserProfileResponse> UpdateMyProfile(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        await new UpdateProfileRequestValidator(clock).ValidateAndThrowAsync(request, ct);
        var user = await Find(userId, ct);
        await ValidateRoleFields(user.Role, request, ct);
        Apply(user, request);
        await repository.SaveAsync(user, request.Version, null, false, ct);
        return UserProfileResponse.From(user);
    }

    public async Task ChangePassword(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        await new ChangePasswordRequestValidator().ValidateAndThrowAsync(request, ct);
        var user = await Find(userId, ct);
        if (!hasher.VerifyPassword(request.OldPassword, user.PasswordHash))
            throw new ValidationException("Unable to change password. Check your input.");
        var version = user.Version;
        user.ChangePasswordHash(hasher.HashPassword(request.NewPassword), clock.GetUtcNow().UtcDateTime);
        await repository.SaveAsync(user, version, null, true, ct);
    }

    public async Task<UserProfileResponse> AdminUpdateUser(Guid actorId, Guid userId, AdminUpdateUserRequest request, CancellationToken ct)
    {
        await new AdminUpdateUserRequestValidator(clock).ValidateAndThrowAsync(request, ct);
        var actor = await Find(actorId, ct);
        if (actor.Role != UserRole.Admin || actor.Status != UserStatus.Active) throw new ForbiddenException("Administrator access required.");
        var user = await Find(userId, ct);
        if (actorId == userId && (request.Role != UserRole.Admin || request.Status != UserStatus.Active))
            throw new ForbiddenException("Administrators cannot remove their own access.");
        await ValidateRoleFields(request.Role, request, ct);
        var before = JsonSerializer.Serialize(UserProfileResponse.From(user));
        var revoke = user.Role != request.Role || user.Status != request.Status;
        user.UpdateAccess(request.Role, request.Status, clock.GetUtcNow().UtcDateTime);
        Apply(user, request);
        var after = JsonSerializer.Serialize(UserProfileResponse.From(user) with { Version = checked(request.Version + 1) });
        var audit = AuditLog.Create(actorId, AuditAction.UserUpdated, userId, null, "{}", clock.GetUtcNow().UtcDateTime);
        audit.SetChanges(before, after);
        await repository.SaveAsync(user, request.Version, audit, revoke, ct);
        return UserProfileResponse.From(user);
    }

    private async Task ValidateRoleFields(UserRole role, UpdateProfileRequest r, CancellationToken ct)
    {
        if (role != UserRole.Student && r.AdministrativeClassId is not null ||
            role != UserRole.Teacher && (r.FacultyId is not null || r.AcademicTitle is not null))
            throw new ValidationException("Profile fields are not allowed for this role.");
        if (r.AdministrativeClassId is Guid classId && !await repository.ClassExistsAsync(classId, ct))
            throw new ValidationException("Administrative class does not exist.");
        if (r.FacultyId is Guid facultyId && !await repository.FacultyExistsAsync(facultyId, ct))
            throw new ValidationException("Faculty does not exist.");
    }
    private void Apply(User user, UpdateProfileRequest r) => user.UpdateProfile(r.FullName, r.PhoneNumber,
        r.DateOfBirth, r.Gender, r.AvatarUrl, r.AdministrativeClassId, r.FacultyId, r.AcademicTitle, clock.GetUtcNow().UtcDateTime);

    public async Task<UserProfileResponse> UploadAvatar(Guid userId, byte[] content, string fileName, int version, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var valid = extension is ".jpg" or ".jpeg" && content.Length >= 3 && content[0] == 255 && content[1] == 216 && content[2] == 255
            || extension == ".png" && content.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10})
            || extension == ".webp" && content.Length >= 12 && content.AsSpan(0,4).SequenceEqual("RIFF"u8) && content.AsSpan(8,4).SequenceEqual("WEBP"u8);
        if (content.Length is 0 or > 2097152 || !valid || version <= 0)
            throw new ValidationException("Avatar must be a JPG, PNG or WebP image of at most 2 MB, with a valid version.");
        var user = await Find(userId, ct);
        if (user.Version != version) throw new ConflictException("Profile changed. Reload and try again.");
        var url = await avatars.UploadAsync(userId, content, extension, ct);
        user.UpdateAvatar(url, clock.GetUtcNow().UtcDateTime);
        await repository.SaveAsync(user, version, null, false, ct);
        return UserProfileResponse.From(user);
    }
}
