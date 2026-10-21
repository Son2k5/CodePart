using CodePath.Application.Auth.Commands;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Users.Entities;
using Xunit;

namespace CodePath.Unit.Tests.Auth;

public sealed class UpdateProfileTests
{
    [Fact]
    public void RenamePreservesAccountIdentityAndPermissions()
    {
        var now = DateTime.UtcNow;
        var user = User.CreateStudent("Original", "123@hanu.edu.vn", "password-hash", "123", now);
        var id = user.Id;
        var role = user.Role;
        var status = user.Status;
        user.UpdateFullName("  New name  ", now);
        Assert.Equal("New name", user.FullName);
        Assert.Equal(id, user.Id);
        Assert.Equal("123@hanu.edu.vn", user.Email);
        Assert.Equal("password-hash", user.PasswordHash);
        Assert.Equal("123", user.StudentId);
        Assert.Equal(role, user.Role);
        Assert.Equal(status, user.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void InvalidNamesAreRejectedByValidatorAndDomain(int length)
    {
        var name = length == 0 ? "   " : new string('a', length);
        var command = new UpdateProfileCommand(Guid.NewGuid(), name);
        Assert.False(new UpdateProfileCommandValidator().Validate(command).IsValid);
        var now = DateTime.UtcNow;
        var user = User.CreateStudent("Original", "123@hanu.edu.vn", "hash", "123", now);
        Assert.Throws<DomainValidationException>(() => user.UpdateFullName(name, now));
        Assert.Equal("Original", user.FullName);
    }
}
