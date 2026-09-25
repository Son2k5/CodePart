using CodePath.Application.Users.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserAuthDto>>;
