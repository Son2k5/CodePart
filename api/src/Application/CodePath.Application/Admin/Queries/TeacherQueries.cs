using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Admin.Dtos;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Admin.Queries;

public sealed record GetTeachersPagedQuery(TeacherFilter Filter) : IRequest<Result<OffsetPage<TeacherDto>>>;
public sealed record GetTeacherStatsQuery : IRequest<Result<TeacherStatsDto>>;
public sealed record GetTeacherDetailQuery(Guid Id) : IRequest<Result<TeacherDetailDto>>;
public sealed record GetFacultiesQuery : IRequest<Result<IReadOnlyList<FacultyDto>>>;

public sealed class TeacherFilterValidator : AbstractValidator<TeacherFilter>
{
    public TeacherFilterValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.FacultyId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Status).Must(x => x is null || Enum.IsDefined(x.Value));
        RuleFor(x => x.SortBy).Must(x => new[] { "name_asc", "name_desc", "created_asc", "created_desc" }.Contains(x));
    }
}
public sealed class GetTeachersPagedQueryValidator : AbstractValidator<GetTeachersPagedQuery>
{
    public GetTeachersPagedQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new TeacherFilterValidator());
}
internal sealed class TeacherQueryHandlers(IAdminDbContext db) :
    IRequestHandler<GetTeachersPagedQuery, Result<OffsetPage<TeacherDto>>>,
    IRequestHandler<GetTeacherStatsQuery, Result<TeacherStatsDto>>,
    IRequestHandler<GetTeacherDetailQuery, Result<TeacherDetailDto>>,
    IRequestHandler<GetFacultiesQuery, Result<IReadOnlyList<FacultyDto>>>
{
    public async Task<Result<OffsetPage<TeacherDto>>> Handle(GetTeachersPagedQuery r, CancellationToken ct) =>
        Result<OffsetPage<TeacherDto>>.Success(await db.GetPagedTeachersAsync(r.Filter, ct));
    public async Task<Result<TeacherStatsDto>> Handle(GetTeacherStatsQuery r, CancellationToken ct) =>
        Result<TeacherStatsDto>.Success(await db.GetTeacherStatsAsync(ct));
    public async Task<Result<TeacherDetailDto>> Handle(GetTeacherDetailQuery r, CancellationToken ct)
    {
        var detail = await db.GetTeacherDetailByIdAsync(r.Id, ct);
        return detail is null ? Result<TeacherDetailDto>.Failure("Teacher not found.", ErrorCodes.NotFound)
            : Result<TeacherDetailDto>.Success(detail);
    }
    public async Task<Result<IReadOnlyList<FacultyDto>>> Handle(GetFacultiesQuery r, CancellationToken ct) =>
        Result<IReadOnlyList<FacultyDto>>.Success(await db.GetFacultiesAsync(ct));
}
