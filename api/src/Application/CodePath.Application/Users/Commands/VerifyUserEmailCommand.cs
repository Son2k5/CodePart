using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Users.Commands;

public sealed record VerifyUserEmailCommand(string Email) : IRequest<Result<bool>>;
