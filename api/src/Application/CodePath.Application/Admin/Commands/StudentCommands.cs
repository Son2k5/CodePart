using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Application.Auth;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Users.Enums;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Commands;

public sealed record CreateStudentCommand(Guid ActorId, CreateStudentInput Data, string? IpAddress) : IRequest<Result<Guid>>;
public sealed record UpdateStudentCommand(Guid ActorId, Guid Id, StudentProfileInput Data, string? IpAddress) : IRequest<Result<Guid>>;
public sealed record BulkStudentsCommand(Guid ActorId, Guid[] UserIds, bool Approve, string? IpAddress) : IRequest<Result<int>>;
public sealed record DeleteStudentCommand(Guid ActorId, Guid Id, string? IpAddress) : IRequest<Result<bool>>;
public sealed record BulkDeleteStudentsCommand(Guid ActorId, Guid[] UserIds, string? IpAddress) : IRequest<Result<int>>;

public sealed class StudentProfileValidator : AbstractValidator<StudentProfileInput>
{
    public StudentProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StudentId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FacultyId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Phone).MaximumLength(20).Matches(@"^\+?[0-9 ()-]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Bio).MaximumLength(1000);
    }
}

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Data).NotNull();
        When(x => x.Data != null, () =>
        {
            RuleFor(x => new StudentProfileInput(x.Data.FullName, x.Data.StudentId, x.Data.FacultyId, x.Data.Phone, x.Data.Bio))
                .SetValidator(new StudentProfileValidator());
            RuleFor(x => x.Data.Email).NotEmpty().MaximumLength(256).EmailAddress()
                .Must(x => x != null && x.Trim().EndsWith("@hanu.edu.vn", StringComparison.OrdinalIgnoreCase));
            RuleFor(x => x.Data.TemporaryPassword).Must(PasswordPolicy.IsValid)
                .WithMessage("Password must have 8–128 characters, uppercase, lowercase, a digit and a symbol.");
            RuleFor(x => x.Data.Status).Must(x => x is UserStatus.Active or UserStatus.Pending);
        });
    }
}

public sealed class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Data).NotNull().SetValidator(new StudentProfileValidator());
    }
}

public sealed class BulkStudentsCommandValidator : AbstractValidator<BulkStudentsCommand>
{
    public BulkStudentsCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.UserIds).NotEmpty().Must(x => x != null && x.Length <= 100 && x.Distinct().Count() == x.Length)
            .WithMessage("Select 1 to 100 distinct students.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}

public sealed class DeleteStudentCommandValidator : AbstractValidator<DeleteStudentCommand>
{
    public DeleteStudentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class BulkDeleteStudentsCommandValidator : AbstractValidator<BulkDeleteStudentsCommand>
{
    public BulkDeleteStudentsCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.UserIds).NotEmpty().Must(x => x != null && x.Length <= 100 && x.Distinct().Count() == x.Length)
            .WithMessage("Select 1 to 100 distinct students.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}

internal sealed class StudentCommandHandlers(IAdminDbContext db, IUsersDbContext users,
    IAuthDbContext auth, IPasswordHasher hasher, TimeProvider clock) :
    IRequestHandler<CreateStudentCommand, Result<Guid>>,
    IRequestHandler<UpdateStudentCommand, Result<Guid>>,
    IRequestHandler<BulkStudentsCommand, Result<int>>,
    IRequestHandler<DeleteStudentCommand, Result<bool>>,
    IRequestHandler<BulkDeleteStudentsCommand, Result<int>>
{
    public async Task<Result<Guid>> Handle(CreateStudentCommand r, CancellationToken ct)
    {
        var hash = hasher.HashPassword(r.Data.TemporaryPassword);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            if (await users.GetByEmailAsync(EmailNormalizer.Normalize(r.Data.Email), token) is not null)
                return Result<Guid>.Failure("Email already exists.", ErrorCodes.Conflict);
            var error = await ValidateReferences(r.Data.StudentId, r.Data.FacultyId, null, token);
            if (error is not null) return error;
            var now = clock.GetUtcNow().UtcDateTime;
            var u = User.CreateStudentByAdmin(r.Data.FullName, r.Data.Email, hash, r.Data.StudentId,
                r.Data.FacultyId, r.Data.Phone, r.Data.Bio, r.Data.Status, now);
            await users.AddAsync(u, token);
            await db.AddAuditLogAsync(AuditLog.Create(r.ActorId, AuditAction.StudentCreated, u.Id,
                r.IpAddress, "{}", now), token);
            await users.SaveChangesAsync(token);
            return Result<Guid>.Success(u.Id);
        }, ct);
    }

    public Task<Result<Guid>> Handle(UpdateStudentCommand r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var u = await users.GetByIdAsync(r.Id, token);
        if (u is null || u.Role != UserRole.Student) return Result<Guid>.Failure("Student not found.", ErrorCodes.NotFound);
        var error = await ValidateReferences(r.Data.StudentId, r.Data.FacultyId, r.Id, token);
        if (error is not null) return error;
        var now = clock.GetUtcNow().UtcDateTime;
        u.UpdateStudentProfile(r.Data.FullName, r.Data.StudentId, r.Data.FacultyId, r.Data.Phone, r.Data.Bio, now);
        await db.AddAuditLogAsync(AuditLog.Create(r.ActorId, AuditAction.StudentUpdated, u.Id, r.IpAddress, "{}", now), token);
        await users.SaveChangesAsync(token);
        return Result<Guid>.Success(u.Id);
    }, ct);

    public Task<Result<int>> Handle(BulkStudentsCommand r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var targets = new List<User>();
        foreach (var id in r.UserIds.Order())
        {
            var u = await users.GetByIdAsync(id, token);
            if (u is null || u.Role != UserRole.Student)
                return Result<int>.Failure($"Student {id} not found.", ErrorCodes.NotFound);
            if (u.Status != (r.Approve ? UserStatus.Pending : UserStatus.Active))
                return Result<int>.Failure($"Student {id} is not eligible for this action.", ErrorCodes.Conflict);
            targets.Add(u);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var u in targets)
        {
            var previous = u.Status;
            var revoked = 0;
            if (r.Approve) u.ApproveStudent(now);
            else
            {
                u.Disable(r.ActorId, now);
                revoked = await auth.RevokeAllUserRefreshTokensAsync(u.Id, now, r.IpAddress, token);
            }
            var metadata = System.Text.Json.JsonSerializer.Serialize(new {
                fromStatus = previous.ToString(), toStatus = u.Status.ToString(), role = "Student", revokedSessionCount = revoked });
            await db.AddAuditLogAsync(AuditLog.Create(r.ActorId,
                r.Approve ? AuditAction.StudentApproved : AuditAction.UserDisabled, u.Id, r.IpAddress, metadata, now), token);
        }
        await users.SaveChangesAsync(token);
        return Result<int>.Success(targets.Count);
    }, ct);

    public Task<Result<bool>> Handle(DeleteStudentCommand r, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(token => db.DeleteUserAsync(r.ActorId, r.Id, r.IpAddress, token), ct);

    public Task<Result<int>> Handle(BulkDeleteStudentsCommand r, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        var count = 0;
        foreach (var id in r.UserIds.Order())
        {
            var res = await db.DeleteUserAsync(r.ActorId, id, r.IpAddress, token);
            if (!res.IsSuccess) return Result<int>.Failure(res.Error ?? "Xóa sinh viên thất bại.", res.ErrorCode);
            count++;
        }
        return Result<int>.Success(count);
    }, ct);

    private async Task<Result<Guid>?> ValidateReferences(string? code, Guid? facultyId, Guid? exceptId, CancellationToken ct)
    {
        if (facultyId.HasValue && !await db.FacultyExistsAsync(facultyId.Value, ct))
            return Result<Guid>.Failure("Faculty not found.", ErrorCodes.NotFound);
        if (!string.IsNullOrWhiteSpace(code) && await db.StudentCodeExistsAsync(code.Trim(), exceptId, ct))
            return Result<Guid>.Failure("Student ID already exists.", ErrorCodes.Conflict);
        return null;
    }
}
