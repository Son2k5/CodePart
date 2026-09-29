using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

/// <summary>
/// Query nội bộ cho flow đăng nhập — trả kèm PasswordHash. Không dùng ở Api.
/// </summary>
public sealed record GetUserCredentialsByEmailQuery(string Email) : IRequest<Result<UserCredentialsDto>>;
