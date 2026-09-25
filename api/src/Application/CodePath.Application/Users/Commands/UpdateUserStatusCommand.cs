using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using MediatR;

namespace CodePath.Application.Users.Commands;

public sealed record UpdateUserStatusCommand(Guid UserId, UserStatus NewStatus) : IRequest<Result<bool>>;
