using CodePath.Shared.Kernel.Enums;

namespace CodePath.Application.Users.Dtos;

/// <summary>
/// DTO public cho Application/Api — KHÔNG chứa PasswordHash.
/// </summary>
public sealed record UserAuthDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    UserStatus Status,
    string? StudentId,
    DateTime? EmailVerifiedAt);

/// <summary>
/// DTO nội bộ cho flow xác thực mật khẩu (chỉ Login dùng).
/// </summary>
public sealed record UserCredentialsDto(
    Guid Id,
    string Email,
    string FullName,
    string PasswordHash,
    UserRole Role,
    UserStatus Status,
    DateTime? EmailVerifiedAt);
