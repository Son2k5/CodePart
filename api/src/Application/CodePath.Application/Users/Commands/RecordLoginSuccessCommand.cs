using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Commands;

public sealed record RecordLoginSuccessCommand(Guid UserId) : IRequest<Result<bool>>;
