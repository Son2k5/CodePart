using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record GetStudentsPagedQuery(StudentFilter Filter) : IRequest<Result<OffsetPage<StudentDto>>>;
public sealed record GetStudentStatsQuery : IRequest<Result<StudentStatsDto>>;
public sealed record GetStudentDetailQuery(Guid Id) : IRequest<Result<StudentDetailDto>>;

public sealed class StudentFilterValidator : AbstractValidator<StudentFilter>
{
    public StudentFilterValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.FacultyId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Status).Must(x => x is null || Enum.IsDefined(x.Value));
        RuleFor(x => x.SortBy).Must(x => new[] { "name_asc", "name_desc", "created_asc", "created_desc" }.Contains(x));
    }
}

public sealed class GetStudentsPagedQueryValidator : AbstractValidator<GetStudentsPagedQuery>
{
    public GetStudentsPagedQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new StudentFilterValidator());
}

internal sealed class StudentQueryHandlers(IAdminDbContext db) :
    IRequestHandler<GetStudentsPagedQuery, Result<OffsetPage<StudentDto>>>,
    IRequestHandler<GetStudentStatsQuery, Result<StudentStatsDto>>,
    IRequestHandler<GetStudentDetailQuery, Result<StudentDetailDto>>
{
    public async Task<Result<OffsetPage<StudentDto>>> Handle(GetStudentsPagedQuery r, CancellationToken ct) =>
        Result<OffsetPage<StudentDto>>.Success(await db.GetPagedStudentsAsync(r.Filter, ct));

    public async Task<Result<StudentStatsDto>> Handle(GetStudentStatsQuery r, CancellationToken ct) =>
        Result<StudentStatsDto>.Success(await db.GetStudentStatsAsync(ct));

    public async Task<Result<StudentDetailDto>> Handle(GetStudentDetailQuery r, CancellationToken ct)
    {
        var detail = await db.GetStudentDetailByIdAsync(r.Id, ct);
        return detail is null ? Result<StudentDetailDto>.Failure("Student not found.", ErrorCodes.NotFound)
            : Result<StudentDetailDto>.Success(detail);
    }
}
