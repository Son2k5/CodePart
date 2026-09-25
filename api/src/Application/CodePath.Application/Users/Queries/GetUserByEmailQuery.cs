using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

public sealed record GetUserByEmailQuery(string Email) : IRequest<Result<UserAuthDto>>;
