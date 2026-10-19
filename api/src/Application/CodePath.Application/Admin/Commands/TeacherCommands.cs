using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Application.Auth;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Commands;

public sealed record CreateTeacherCommand(Guid ActorId, CreateTeacherInput Data, string? IpAddress) : IRequest<Result<Guid>>;
public sealed record UpdateTeacherCommand(Guid ActorId, Guid Id, TeacherProfileInput Data, string? IpAddress) : IRequest<Result<Guid>>;
public sealed record EnableUserCommand(Guid ActorId, Guid UserId, string? IpAddress) : IRequest<Result<AdminUserStatusChangeDto>>;
public sealed record BulkTeachersCommand(Guid ActorId, Guid[] UserIds, bool Approve, string? IpAddress) : IRequest<Result<int>>;

public sealed class TeacherProfileValidator : AbstractValidator<TeacherProfileInput>
{
    public TeacherProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TeacherId).MaximumLength(50);
        RuleFor(x => x.FacultyId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Phone).MaximumLength(20).Matches(@"^\+?[0-9 ()-]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Bio).MaximumLength(1000);
    }
}
public sealed class CreateTeacherCommandValidator : AbstractValidator<CreateTeacherCommand>
{
    public CreateTeacherCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Data).NotNull();
        When(x => x.Data != null, () =>
        {
            RuleFor(x => new TeacherProfileInput(x.Data.FullName, x.Data.TeacherId, x.Data.FacultyId, x.Data.Phone, x.Data.Bio))
                .SetValidator(new TeacherProfileValidator());
            RuleFor(x => x.Data.Email).NotEmpty().MaximumLength(256).EmailAddress()
                .Must(x => x != null && x.Trim().EndsWith("@hanu.edu.vn", StringComparison.OrdinalIgnoreCase));
            RuleFor(x => x.Data.TemporaryPassword).Must(PasswordPolicy.IsValid)
                .WithMessage("Password must have 8–128 characters, uppercase, lowercase, a digit and a symbol.");
            RuleFor(x => x.Data.Status).Must(x => x is UserStatus.Active or UserStatus.Pending);
        });
    }
}
public sealed class UpdateTeacherCommandValidator : AbstractValidator<UpdateTeacherCommand>
{
    public UpdateTeacherCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).NotNull().SetValidator(new TeacherProfileValidator());
    }
}
public sealed class EnableUserCommandValidator : AbstractValidator<EnableUserCommand>
{
    public EnableUserCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
public sealed class BulkTeachersCommandValidator : AbstractValidator<BulkTeachersCommand>
{
    public BulkTeachersCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.UserIds).NotEmpty().Must(x => x != null && x.Length <= 100 && x.Distinct().Count() == x.Length)
            .WithMessage("Select 1 to 100 distinct teachers.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}

internal sealed class TeacherCommandHandlers(IAdminDbContext db, IUsersDbContext users,
    IAuthDbContext auth, IPasswordHasher hasher, TimeProvider clock, AdminStatusChangeService statusService) :
    IRequestHandler<CreateTeacherCommand, Result<Guid>>,
    IRequestHandler<UpdateTeacherCommand, Result<Guid>>,
    IRequestHandler<EnableUserCommand, Result<AdminUserStatusChangeDto>>,
    IRequestHandler<BulkTeachersCommand, Result<int>>
{
    public async Task<Result<Guid>> Handle(CreateTeacherCommand r, CancellationToken ct)
    {
        // Hash once, outside the retried database transaction.
        var hash = hasher.HashPassword(r.Data.TemporaryPassword);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            if (await users.GetByEmailAsync(EmailNormalizer.Normalize(r.Data.Email), token) is not null)
                return Result<Guid>.Failure("Email already exists.", ErrorCodes.Conflict);
            var error = await ValidateReferences(r.Data.TeacherId, r.Data.FacultyId, null, token);
            if (error is not null) return error;
            var now = clock.GetUtcNow().UtcDateTime;
            var u = User.CreateTeacherByAdmin(r.Data.FullName, r.Data.Email, hash, r.Data.TeacherId,
                r.Data.FacultyId, r.Data.Phone, r.Data.Bio, r.Data.Status, now);
            await users.AddAsync(u, token);
            await db.AddAuditLogAsync(AuditLog.Create(r.ActorId, AuditAction.TeacherCreated, u.Id,
                r.IpAddress, "{}", now), token);
            await users.SaveChangesAsync(token);
            return Result<Guid>.Success(u.Id);
        }, ct);
    }

    public Task<Result<Guid>> Handle(UpdateTeacherCommand r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var u = await users.GetByIdAsync(r.Id, token);
        if (u is null || u.Role != UserRole.Teacher) return Result<Guid>.Failure("Teacher not found.", ErrorCodes.NotFound);
        var error = await ValidateReferences(r.Data.TeacherId, r.Data.FacultyId, r.Id, token);
        if (error is not null) return error;
        var now = clock.GetUtcNow().UtcDateTime;
        u.UpdateTeacherProfile(r.Data.FullName, r.Data.TeacherId, r.Data.FacultyId, r.Data.Phone, r.Data.Bio, now);
        await db.AddAuditLogAsync(AuditLog.Create(r.ActorId, AuditAction.TeacherUpdated, u.Id, r.IpAddress, "{}", now), token);
        await users.SaveChangesAsync(token);
        return Result<Guid>.Success(u.Id);
    }, ct);

    public Task<Result<AdminUserStatusChangeDto>> Handle(EnableUserCommand r, CancellationToken ct) =>
        statusService.ChangeAsync(r.ActorId, r.UserId, AdminStatusAction.EnableUser, r.IpAddress, ct);

    public Task<Result<int>> Handle(BulkTeachersCommand r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var targets = new List<User>();
        foreach (var id in r.UserIds.Order())
        {
            var u = await users.GetByIdAsync(id, token);
            if (u is null || u.Role != UserRole.Teacher)
                return Result<int>.Failure($"Teacher {id} not found.", ErrorCodes.NotFound);
            if (u.Status != (r.Approve ? UserStatus.Pending : UserStatus.Active) || (r.Approve && u.EmailVerifiedAt is null))
                return Result<int>.Failure($"Teacher {id} is not eligible for this action.", ErrorCodes.Conflict);
            targets.Add(u);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var u in targets)
        {
            var previous = u.Status;
            var revoked = 0;
            if (r.Approve) u.ApproveTeacher(now);
            else
            {
                u.Disable(r.ActorId, now);
                revoked = await auth.RevokeAllUserRefreshTokensAsync(u.Id, now, r.IpAddress, token);
            }
            var metadata = System.Text.Json.JsonSerializer.Serialize(new {
                fromStatus = previous.ToString(), toStatus = u.Status.ToString(), role = "Teacher", revokedSessionCount = revoked });
            await db.AddAuditLogAsync(AuditLog.Create(r.ActorId,
                r.Approve ? AuditAction.TeacherApproved : AuditAction.UserDisabled, u.Id, r.IpAddress, metadata, now), token);
        }
        await users.SaveChangesAsync(token);
        return Result<int>.Success(targets.Count);
    }, ct);

    private async Task<Result<Guid>?> ValidateReferences(string? code, Guid? facultyId, Guid? exceptId, CancellationToken ct)
    {
        if (facultyId.HasValue && !await db.FacultyExistsAsync(facultyId.Value, ct))
            return Result<Guid>.Failure("Faculty not found.", ErrorCodes.NotFound);
        if (!string.IsNullOrWhiteSpace(code) && await db.TeacherCodeExistsAsync(code.Trim().ToUpperInvariant(), exceptId, ct))
            return Result<Guid>.Failure("Teacher ID already exists.", ErrorCodes.Conflict);
        return null;
    }
}
