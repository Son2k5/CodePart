using CodePath.Domain.Users.Entities;
using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Classes;

public sealed record SaveClassCommand(Guid Actor, Guid? Id, ClassInput Data) : IRequest<Result<Guid>>;
public enum ClassAction { Delete, Status, AddTeachers, RemoveTeacher, RegenerateCode, RevokeCode, RemoveStudent }
public sealed record ChangeClassCommand(Guid Actor, Guid Id, ClassAction Action, Guid[]? TeacherIds = null,
    Guid? Subject = null, ClassStatus? Status = null, DateTime? ExpiresAt = null,
    bool Force = false, bool Confirm = false) : IRequest<Result<Guid>>;
public sealed record JoinClassCommand(Guid Student, Guid? Id, string? Code) : IRequest<Result<Guid>>;
public sealed record LeaveClassCommand(Guid Student, Guid Id) : IRequest<Result<Guid>>;

public sealed class ClassInputValidator : AbstractValidator<ClassInput>
{
    public ClassInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(x => x != null && x.Trim().Length is >= 3 and <= 100);
        RuleFor(x => x.Description).NotNull().MaximumLength(5000);
        RuleFor(x => x.Thumbnail).MaximumLength(2048).Must(x => string.IsNullOrEmpty(x) ||
            System.Text.RegularExpressions.Regex.IsMatch(x, @"\A/api/class-images/[a-f0-9]{32}\.(png|jpg|webp)\z") ||
            Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http");
        RuleFor(x => x.Status).IsInEnum(); RuleFor(x => x.AccessType).IsInEnum();
        RuleFor(x => x.MaxStudents).GreaterThan(0).When(x => x.MaxStudents.HasValue);
        RuleFor(x => x.EndDate).Must((x, end) => !end.HasValue || !x.StartDate.HasValue || end > x.StartDate);
        RuleFor(x => x.TeacherIds).Must(x => x == null || x.Length <= 100);
        RuleForEach(x => x.TeacherIds).NotEmpty();
    }
}
public sealed class SaveClassValidator : AbstractValidator<SaveClassCommand>
{
    public SaveClassValidator(TimeProvider clock)
    {
        RuleFor(x => x.Data).NotNull().SetValidator(new ClassInputValidator());
        When(x => x.Data != null, () => {
            RuleFor(x => x.Data.RowVersion).NotEmpty().When(x => x.Id.HasValue);
            RuleFor(x => x.Data.JoinCodeExpiresAt).Must(x => !x.HasValue || x.Value > clock.GetUtcNow().UtcDateTime);
        });
    }
}
public sealed class ChangeClassValidator : AbstractValidator<ChangeClassCommand>
{
    public ChangeClassValidator(TimeProvider clock)
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Status).NotNull().IsInEnum().When(x => x.Action == ClassAction.Status);
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Action is ClassAction.RemoveStudent or ClassAction.RemoveTeacher);
        RuleFor(x => x.TeacherIds).NotEmpty().Must(x => x != null && x.Length <= 100).When(x => x.Action == ClassAction.AddTeachers);
        RuleForEach(x => x.TeacherIds).NotEmpty();
        RuleFor(x => x.ExpiresAt).Must(x => !x.HasValue || x > clock.GetUtcNow().UtcDateTime);
    }
}
public sealed class JoinClassValidator : AbstractValidator<JoinClassCommand>
{
    public JoinClassValidator()
    {
        RuleFor(x => x).Must(x => x.Id.HasValue != (x.Code != null));
        RuleFor(x => x.Code).Must(x => x != null && x.Trim().Length == 8 &&
            x.Trim().ToUpperInvariant().All(JoinCodeGenerator.Alphabet.Contains)).When(x => !x.Id.HasValue);
    }
}

public sealed class ClassCommandHandlers(IClassRepository db, TimeProvider clock, JoinCodeGenerator codes) :
    IRequestHandler<SaveClassCommand, Result<Guid>>, IRequestHandler<ChangeClassCommand, Result<Guid>>,
    IRequestHandler<JoinClassCommand, Result<Guid>>, IRequestHandler<LeaveClassCommand, Result<Guid>>
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<Class> Find(Guid id, CancellationToken ct) => await db.LockAsync(id, null, ct)
        ?? throw new DomainRuleViolationException("Không tìm thấy lớp.", "CLASS_NOT_FOUND");
    private async Task AddTeachers(Class c, Guid[] ids, Guid actor, CancellationToken ct)
    {
        if (ids.Length > 0) c.EnsureTeacherAssignmentAllowed();
        foreach (var id in ids.Distinct())
        {
            Class.EnsureTeacher(await db.RoleAsync(id, ct));
            if (c.Teachers.All(t => t.TeacherId != id))
            {
                c.Teachers.Add(ClassTeacher.Create(c.Id, id, actor, Now));
                await db.AuditAsync(actor, c.Id, "TeacherAdded", id, Now, ct);
            }
        }
    }
    public Task<Result<Guid>> Handle(SaveClassCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var c = r.Id.HasValue ? await Find(r.Id.Value, token) : Class.CreateManaged(r.Data.Name, r.Actor, Now);
        if (c.IsTemplate) return Result<Guid>.Failure("Use curriculum-templates to manage templates.", "CLASS_IS_TEMPLATE");
        if (r.Id.HasValue && c.RowVersion != r.Data.RowVersion)
            return Result<Guid>.Failure("Lớp đã được thay đổi. Vui lòng tải lại.", "CLASS_CONCURRENCY_CONFLICT");
        var d = r.Data;
        if (d.MaxStudents.HasValue && await db.CountStudentsAsync(c.Id, token) > d.MaxStudents)
            return Result<Guid>.Failure("Giới hạn thấp hơn số học sinh hiện tại.", "CLASS_FULL");
        var code = d.AccessType == ClassAccessType.CodeRequired && (!r.Id.HasValue || c.AccessType != d.AccessType)
            ? await codes.GenerateAsync(db.CodeExistsAsync, token) : null;
        c.Configure(d.Name, d.Description, d.Thumbnail, d.AccessType, d.MaxStudents, d.StartDate, d.EndDate, code, d.JoinCodeExpiresAt, Now);
        c.SetStatus(d.Status, Now);
        if (d.TeacherIds != null)
        {
            await AddTeachers(c, d.TeacherIds, r.Actor, token);
            foreach (var t in c.Teachers.Where(t => !d.TeacherIds.Contains(t.TeacherId)).ToList())
            {
                c.Teachers.Remove(t);
                await db.AuditAsync(r.Actor, c.Id, "TeacherRemoved", t.TeacherId, Now, token);
            }
        }
        if (!r.Id.HasValue)
        {
            c.SetMode(c.Teachers.Count > 0 ? ClassMode.Managed : ClassMode.SelfStudy, Now);
            await db.AddAsync(c, token);
        }
        await db.AuditAsync(r.Actor, c.Id, r.Id.HasValue ? "Updated" : "Created", null, Now, token);
        if (d.TeacherIds != null) await db.AuditAsync(r.Actor, c.Id, "TeachersChanged", null, Now, token);
        await db.SaveAsync(token);
        return Result<Guid>.Success(c.Id);
    }, ct);
    public Task<Result<Guid>> Handle(ChangeClassCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var c = await Find(r.Id, token);
        if (c.IsTemplate) return Result<Guid>.Failure("Use curriculum-templates to manage templates.", "CLASS_IS_TEMPLATE");
        switch (r.Action)
        {
            case ClassAction.Delete:
                c.Delete(await db.CountStudentsAsync(c.Id, token), r.Force, r.Confirm, Now);
                await db.RemoveAllStudentsAsync(c.Id, token); break;
            case ClassAction.Status: c.SetStatus(r.Status!.Value, Now); break;
            case ClassAction.AddTeachers: await AddTeachers(c, r.TeacherIds!, r.Actor, token); break;
            case ClassAction.RemoveTeacher:
                var teacher = c.Teachers.SingleOrDefault(t => t.TeacherId == r.Subject);
                if (teacher != null) c.Teachers.Remove(teacher); break;
            case ClassAction.RegenerateCode: c.SetJoinCode(await codes.GenerateAsync(db.CodeExistsAsync, token), r.ExpiresAt, Now); break;
            case ClassAction.RevokeCode: c.SetJoinCode(null, null, Now); break;
            case ClassAction.RemoveStudent:
                var enrollment = await db.EnrollmentAsync(c.Id, r.Subject!.Value, token);
                if (enrollment == null) return Result<Guid>.Failure("Học sinh không có trong lớp.", "ENROLLMENT_NOT_FOUND");
                enrollment.Remove(); break;
        }
        c.Changed(Now);
        await db.AuditAsync(r.Actor, c.Id, r.Action.ToString(), r.Subject, Now, token);
        await db.SaveAsync(token);
        return Result<Guid>.Success(c.Id);
    }, ct);
    public Task<Result<Guid>> Handle(JoinClassCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var c = await db.LockAsync(r.Id, r.Code?.Trim().ToUpperInvariant(), token);
        if (c == null) return Result<Guid>.Failure("Không tìm thấy lớp hoặc mã không hợp lệ.", r.Id.HasValue ? "CLASS_NOT_FOUND" : "INVALID_JOIN_CODE");
        var e = await db.EnrollmentAsync(c.Id, r.Student, token);
        c.EnsureCanJoin(r.Code != null, await db.CountStudentsAsync(c.Id, token), e?.Status == ClassEnrollmentStatus.Active, Now);
        var method = r.Code == null ? ClassJoinMethod.Public : ClassJoinMethod.Code;
        if (e == null) await db.AddEnrollmentAsync(ClassEnrollment.Create(c.Id, r.Student, method, Now), token);
        else e.Rejoin(method, Now);
        c.Changed(Now); await db.SaveAsync(token);
        return Result<Guid>.Success(c.Id);
    }, ct);
    public Task<Result<Guid>> Handle(LeaveClassCommand r, CancellationToken ct) => db.TransactionAsync(async token => {
        var c = await Find(r.Id, token);
        var e = await db.EnrollmentAsync(c.Id, r.Student, token);
        e?.Remove(); c.Changed(Now); await db.SaveAsync(token);
        return Result<Guid>.Success(c.Id);
    }, ct);
}
