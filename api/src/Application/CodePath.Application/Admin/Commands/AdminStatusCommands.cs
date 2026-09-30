using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Commands;

public sealed record ApproveTeacherCommand(
    Guid ActorId,
    Guid UserId,
    string? IpAddress) : IRequest<Result<AdminUserStatusChangeDto>>;

public sealed record RejectTeacherCommand(
    Guid ActorId,
    Guid UserId,
    string? IpAddress) : IRequest<Result<AdminUserStatusChangeDto>>;

public sealed record DisableUserCommand(
    Guid ActorId,
    Guid UserId,
    string? IpAddress) : IRequest<Result<AdminUserStatusChangeDto>>;

public sealed class ApproveTeacherCommandValidator : AbstractValidator<ApproveTeacherCommand>
{
    public ApproveTeacherCommandValidator()
    {
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.IpAddress).MaximumLength(45);
    }
}

public sealed class RejectTeacherCommandValidator : AbstractValidator<RejectTeacherCommand>
{
    public RejectTeacherCommandValidator()
    {
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.IpAddress).MaximumLength(45);
    }
}

public sealed class DisableUserCommandValidator : AbstractValidator<DisableUserCommand>
{
    public DisableUserCommandValidator()
    {
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.IpAddress).MaximumLength(45);
    }
}

internal sealed class ApproveTeacherCommandHandler
    : IRequestHandler<ApproveTeacherCommand, Result<AdminUserStatusChangeDto>>
{
    private readonly AdminStatusChangeService _service;

    public ApproveTeacherCommandHandler(AdminStatusChangeService service) => _service = service;

    public Task<Result<AdminUserStatusChangeDto>> Handle(
        ApproveTeacherCommand request,
        CancellationToken cancellationToken) =>
        _service.ChangeAsync(
            request.ActorId,
            request.UserId,
            AdminStatusAction.ApproveTeacher,
            request.IpAddress,
            cancellationToken);
}

internal sealed class RejectTeacherCommandHandler
    : IRequestHandler<RejectTeacherCommand, Result<AdminUserStatusChangeDto>>
{
    private readonly AdminStatusChangeService _service;

    public RejectTeacherCommandHandler(AdminStatusChangeService service) => _service = service;

    public Task<Result<AdminUserStatusChangeDto>> Handle(
        RejectTeacherCommand request,
        CancellationToken cancellationToken) =>
        _service.ChangeAsync(
            request.ActorId,
            request.UserId,
            AdminStatusAction.RejectTeacher,
            request.IpAddress,
            cancellationToken);
}

internal sealed class DisableUserCommandHandler
    : IRequestHandler<DisableUserCommand, Result<AdminUserStatusChangeDto>>
{
    private readonly AdminStatusChangeService _service;

    public DisableUserCommandHandler(AdminStatusChangeService service) => _service = service;

    public Task<Result<AdminUserStatusChangeDto>> Handle(
        DisableUserCommand request,
        CancellationToken cancellationToken) =>
        _service.ChangeAsync(
            request.ActorId,
            request.UserId,
            AdminStatusAction.DisableUser,
            request.IpAddress,
            cancellationToken);
}
