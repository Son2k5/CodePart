using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Classes;

public sealed record ListClassesQuery(ClassFilter Filter) : IRequest<Result<ClassPage<ClassDto>>>;
public sealed record PublicClassesQuery(ClassFilter Filter, Guid Student) : IRequest<Result<ClassPage<PublicClassDto>>>;
public sealed record ClassDetailQuery(Guid Id) : IRequest<Result<ClassDto>>;
public sealed record ClassStudentsQuery(Guid Id, ClassFilter Filter) : IRequest<Result<ClassPage<ClassStudentDto>>>;
public sealed class ClassFilterValidator : AbstractValidator<ClassFilter>
{
    public ClassFilterValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum(); RuleFor(x => x.AccessType).IsInEnum();
        RuleFor(x => x.Sort).Must(x => x is "createdAt" or "name" or "studentCount");
    }
}
public sealed class ListClassesValidator : AbstractValidator<ListClassesQuery>
{ public ListClassesValidator() => RuleFor(x => x.Filter).SetValidator(new ClassFilterValidator()); }
public sealed class PublicClassesValidator : AbstractValidator<PublicClassesQuery>
{ public PublicClassesValidator() => RuleFor(x => x.Filter).SetValidator(new ClassFilterValidator()); }
public sealed class ClassStudentsValidator : AbstractValidator<ClassStudentsQuery>
{ public ClassStudentsValidator() => RuleFor(x => x.Filter).SetValidator(new ClassFilterValidator()); }
public sealed class ClassQueryHandlers(IClassRepository db) :
    IRequestHandler<ListClassesQuery, Result<ClassPage<ClassDto>>>,
    IRequestHandler<PublicClassesQuery, Result<ClassPage<PublicClassDto>>>,
    IRequestHandler<ClassDetailQuery, Result<ClassDto>>,
    IRequestHandler<ClassStudentsQuery, Result<ClassPage<ClassStudentDto>>>
{
    public async Task<Result<ClassPage<ClassDto>>> Handle(ListClassesQuery r, CancellationToken ct) =>
        Result<ClassPage<ClassDto>>.Success(await db.ListAsync(r.Filter, ct));
    public async Task<Result<ClassPage<PublicClassDto>>> Handle(PublicClassesQuery r, CancellationToken ct) =>
        Result<ClassPage<PublicClassDto>>.Success(await db.PublicAsync(r.Filter, r.Student, ct));
    public async Task<Result<ClassDto>> Handle(ClassDetailQuery r, CancellationToken ct) =>
        await db.DetailAsync(r.Id, ct) is { } c ? Result<ClassDto>.Success(c) : Result<ClassDto>.Failure("Không tìm thấy lớp.", "CLASS_NOT_FOUND");
    public async Task<Result<ClassPage<ClassStudentDto>>> Handle(ClassStudentsQuery r, CancellationToken ct) =>
        await db.DetailAsync(r.Id, ct) == null ? Result<ClassPage<ClassStudentDto>>.Failure("Không tìm thấy lớp.", "CLASS_NOT_FOUND") :
        Result<ClassPage<ClassStudentDto>>.Success(await db.StudentsAsync(r.Id, r.Filter, ct));
}
