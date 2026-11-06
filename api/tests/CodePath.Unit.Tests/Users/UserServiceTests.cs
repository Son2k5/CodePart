using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using Moq;
using Xunit;

namespace CodePath.Unit.Tests.Users;

public sealed class UserServiceTests
{
    private readonly Mock<IUserRepository> repository = new();
    private readonly Mock<IPasswordHasher> hasher = new();
    private readonly Mock<IAvatarStorage> avatars = new();
    private readonly User user = User.CreateStudent("Nguyen Van An", "an@example.com", "old-hash", "SV001", DateTime.UtcNow);
    private UserService Service => new(repository.Object, hasher.Object, avatars.Object, TimeProvider.System);
    private UpdateProfileRequest Profile => new("Nguyen Van Binh", "0912345678", new DateOnly(2000, 1, 1), "Male", null, null, null, null, 1);
    public UserServiceTests() => repository.Setup(x => x.GetAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    [Fact]
    public async Task Wrong_password_does_not_hash_or_save()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service.ChangePassword(user.Id, new("wrong", "Newpass123", "Newpass123"), default));
        hasher.Verify(x => x.HashPassword(It.IsAny<string>()), Times.Never);
        repository.Verify(x => x.SaveAsync(It.IsAny<User>(), It.IsAny<int>(), It.IsAny<AuditLog?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Correct_password_updates_hash_and_revokes_all_sessions_atomically()
    {
        hasher.Setup(x => x.VerifyPassword("Oldpass123", "old-hash")).Returns(true);
        hasher.Setup(x => x.HashPassword("Newpass123")).Returns("new-hash");
        await Service.ChangePassword(user.Id, new("Oldpass123", "Newpass123", "Newpass123"), default);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(2, user.SecurityVersion);
        repository.Verify(x => x.SaveAsync(user, 1, null, true, default), Times.Once);
    }
    [Theory]
    [InlineData("short", "short")]
    [InlineData("lowercase123", "lowercase123")]
    [InlineData("UppercaseOnly", "UppercaseOnly")]
    [InlineData("Newpass123", "mismatch")]
    [InlineData("Oldpass123", "Oldpass123")]
    public async Task Invalid_password_is_rejected_without_persistence(string password, string confirmation)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service.ChangePassword(user.Id, new("Oldpass123", password, confirmation), default));
        repository.Verify(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public void Password_validation_does_not_disclose_passwords()
    {
        var result = new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest("Secret123", "Secret123", "mismatch"));
        Assert.All(result.Errors, e => Assert.DoesNotContain("Secret123", e.ErrorMessage));
    }
    [Fact]
    public async Task Valid_profile_preserves_identity_and_password()
    {
        var result = await Service.UpdateMyProfile(user.Id, Profile, default);
        Assert.Equal("Nguyen Van Binh", result.FullName);
        Assert.Equal("an@example.com", result.Email);
        Assert.Equal("SV001", result.StudentCode);
        Assert.DoesNotContain("old-hash", JsonSerializer.Serialize(result));
        repository.Verify(x => x.SaveAsync(user, 1, null, false, default), Times.Once);
    }
    [Theory]
    [InlineData("A", "0912345678")]
    [InlineData("Valid Name", "123")]
    public async Task Invalid_profile_does_not_load_or_save(string name, string phone)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service.UpdateMyProfile(user.Id, Profile with { FullName = name, PhoneNumber = phone }, default));
        repository.Verify(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Future_birth_date_is_rejected() => await Assert.ThrowsAsync<ValidationException>(() => Service.UpdateMyProfile(user.Id, Profile with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }, default));
    [Fact]
    public async Task Missing_class_is_rejected() => await Assert.ThrowsAsync<ValidationException>(() => Service.UpdateMyProfile(user.Id, Profile with { AdministrativeClassId = Guid.NewGuid() }, default));
    [Fact]
    public async Task Student_cannot_set_teacher_fields() => await Assert.ThrowsAsync<ValidationException>(() => Service.UpdateMyProfile(user.Id, Profile with { FacultyId = Guid.NewGuid() }, default));
    [Fact]
    public async Task Existing_class_is_assigned()
    {
        var id = Guid.NewGuid();
        repository.Setup(x => x.ClassExistsAsync(id, default)).ReturnsAsync(true);
        Assert.Equal(id, (await Service.UpdateMyProfile(user.Id, Profile with { AdministrativeClassId = id }, default)).AdministrativeClassId);
    }
    [Fact]
    public async Task Concurrency_conflict_is_propagated()
    {
        repository.Setup(x => x.SaveAsync(user, 1, null, false, default)).ThrowsAsync(new ConflictException("Conflict"));
        await Assert.ThrowsAsync<ConflictException>(() => Service.UpdateMyProfile(user.Id, Profile, default));
    }
    [Theory]
    [InlineData("Email")]
    [InlineData("StudentCode")]
    [InlineData("StudentId")]
    [InlineData("Role")]
    [InlineData("Status")]
    [InlineData("UserId")]
    public void Forbidden_fields_are_rejected_during_binding(string field)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateProfileRequest>("{\"" + field + "\":\"forbidden\"}"));
    }
    [Fact]
    public async Task Admin_update_records_only_safe_snapshots()
    {
        var admin = User.CreateAdmin("Admin User", "admin@example.com", "admin-hash", DateTime.UtcNow);
        repository.Setup(x => x.GetAsync(admin.Id, default)).ReturnsAsync(admin);
        AuditLog? captured = null;
        repository.Setup(x => x.SaveAsync(user, 1, It.IsAny<AuditLog>(), true, default)).Callback<User,int,AuditLog?,bool,CancellationToken>((_,_,a,_,_) => captured = a).Returns(Task.CompletedTask);
        await Service.AdminUpdateUser(admin.Id, user.Id, new(Profile.FullName, null, null, null, null, null, null, null, 1, UserRole.Student, UserStatus.Disabled), default);
        Assert.NotNull(captured);
        Assert.Equal(admin.Id, captured.ActorId);
        Assert.Equal((int)UserStatus.Active, JsonDocument.Parse(captured.OldValue!).RootElement.GetProperty("Status").GetInt32());
        Assert.DoesNotContain("hash", captured.NewValue!);
        Assert.Equal(2, user.SecurityVersion);
    }
    [Theory]
    [InlineData("avatar.jpg")]
    [InlineData("avatar.png")]
    [InlineData("avatar.svg")]
    public async Task Invalid_image_is_rejected_before_upload(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service.UploadAvatar(user.Id, new byte[] {1,2,3}, name, 1, default));
        avatars.Verify(x => x.UploadAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
