using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Users.Dtos;

public sealed record UserAuthDto(
    Guid Id,
    string Email,
    string FullName,
    string PasswordHash,
    UserRole Role,
    UserStatus Status,
    string? StudentId,
    DateTime? EmailVerifiedAt);
