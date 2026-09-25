using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using MediatR;

namespace CodePath.Application.Users.Commands;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string PasswordHash,
    UserRole Role,
    UserStatus Status,
    string? StudentId) : IRequest<Result<Guid>>;
