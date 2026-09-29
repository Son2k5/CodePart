using CodePath.Domain.Users.Entities;

namespace CodePath.Application.Users.Dtos;

public static class UserMappingExtensions
{
    public static UserAuthDto ToUserAuthDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserAuthDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.Status,
            user.StudentId,
            user.EmailVerifiedAt);
    }

    public static UserCredentialsDto ToUserCredentialsDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserCredentialsDto(
            user.Id,
            user.Email,
            user.FullName,
            user.PasswordHash,
            user.Role,
            user.Status,
            user.EmailVerifiedAt);
    }
}
