using CodePath.Application.Teaching.Abstractions;
using CodePath.Application.Teaching.Dtos;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Exceptions;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Teaching.Queries;

public sealed record GetCoursesQuery(Guid UserId, bool Teacher) : IRequest<Result<IReadOnlyList<CourseDto>>>;
public sealed record GetCourseQuery(Guid TeacherId, Guid CourseId) : IRequest<Result<CourseDto>>;
public sealed record GetCourseStudentsQuery(Guid TeacherId, Guid CourseId) : IRequest<Result<IReadOnlyList<StudentDto>>>;
public sealed record GetCourseExercisesQuery(Guid UserId, Guid CourseId, bool Teacher) : IRequest<Result<IReadOnlyList<CourseExerciseDto>>>;
public sealed record GetClassesQuery(Guid TeacherId) : IRequest<Result<IReadOnlyList<ClassDto>>>;
public sealed record GetClassStudentsQuery(Guid TeacherId, Guid ClassId) : IRequest<Result<IReadOnlyList<StudentDto>>>;
public sealed record GetSubmissionsQuery(Guid TeacherId, Guid CourseId, int Page = 1, int PageSize = 20) : IRequest<Result<SubmissionPage>>;
public sealed record GetSubmissionQuery(Guid TeacherId, Guid CourseId, Guid Id) : IRequest<Result<SubmissionDetailDto>>;
public sealed class GetSubmissionsQueryValidator : AbstractValidator<GetSubmissionsQuery>
{
    public GetSubmissionsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
internal sealed class TeachingQueryHandlers(ITeachingDbContext db) :
    IRequestHandler<GetCoursesQuery, Result<IReadOnlyList<CourseDto>>>,
    IRequestHandler<GetCourseQuery, Result<CourseDto>>,
    IRequestHandler<GetCourseStudentsQuery, Result<IReadOnlyList<StudentDto>>>,
    IRequestHandler<GetCourseExercisesQuery, Result<IReadOnlyList<CourseExerciseDto>>>,
    IRequestHandler<GetClassesQuery, Result<IReadOnlyList<ClassDto>>>,
    IRequestHandler<GetClassStudentsQuery, Result<IReadOnlyList<StudentDto>>>,
    IRequestHandler<GetSubmissionsQuery, Result<SubmissionPage>>,
    IRequestHandler<GetSubmissionQuery, Result<SubmissionDetailDto>>
{
    private async Task Own(Guid course, Guid teacher, CancellationToken ct)
    {
        if (await db.OwnedCourseAsync(course, teacher, ct) is null) throw new NotFoundException("Course not found.");
    }
    public async Task<Result<IReadOnlyList<CourseDto>>> Handle(GetCoursesQuery r, CancellationToken ct) =>
        Result<IReadOnlyList<CourseDto>>.Success(await db.CoursesAsync(r.UserId, r.Teacher, ct));
    public async Task<Result<CourseDto>> Handle(GetCourseQuery r, CancellationToken ct)
    {
        var courses = await db.CoursesAsync(r.TeacherId, true, ct);
        var course = courses.FirstOrDefault(c => c.Id == r.CourseId);
        return course is null ? Result<CourseDto>.Failure("Course not found.", ErrorCodes.NotFound) : Result<CourseDto>.Success(course);
    }
    public async Task<Result<IReadOnlyList<StudentDto>>> Handle(GetCourseStudentsQuery r, CancellationToken ct)
    {
        await Own(r.CourseId, r.TeacherId, ct);
        return Result<IReadOnlyList<StudentDto>>.Success(await db.StudentsAsync(r.CourseId, ct));
    }
    public async Task<Result<IReadOnlyList<CourseExerciseDto>>> Handle(GetCourseExercisesQuery r, CancellationToken ct)
    {
        if (r.Teacher) await Own(r.CourseId, r.UserId, ct);
        else if (!await db.EnrolledAsync(r.CourseId, r.UserId, ct)) throw new NotFoundException("Course not found.");
        return Result<IReadOnlyList<CourseExerciseDto>>.Success(await db.ExercisesAsync(r.CourseId, ct));
    }
    public async Task<Result<IReadOnlyList<ClassDto>>> Handle(GetClassesQuery r, CancellationToken ct) =>
        Result<IReadOnlyList<ClassDto>>.Success(await db.ClassesAsync(r.TeacherId, ct));
    public async Task<Result<IReadOnlyList<StudentDto>>> Handle(GetClassStudentsQuery r, CancellationToken ct)
    {
        var students = await db.ClassStudentsAsync(r.ClassId, r.TeacherId, ct);
        return students is null ? Result<IReadOnlyList<StudentDto>>.Failure("Class not found.", ErrorCodes.NotFound)
            : Result<IReadOnlyList<StudentDto>>.Success(students);
    }
    public async Task<Result<SubmissionPage>> Handle(GetSubmissionsQuery r, CancellationToken ct)
    {
        await Own(r.CourseId, r.TeacherId, ct);
        return Result<SubmissionPage>.Success(await db.SubmissionsAsync(r.CourseId, r.Page, r.PageSize, ct));
    }
    public async Task<Result<SubmissionDetailDto>> Handle(GetSubmissionQuery r, CancellationToken ct)
    {
        await Own(r.CourseId, r.TeacherId, ct);
        var detail = await db.SubmissionDetailAsync(r.CourseId, r.Id, ct);
        return detail is null ? Result<SubmissionDetailDto>.Failure("Submission not found.", ErrorCodes.NotFound) : Result<SubmissionDetailDto>.Success(detail);
    }
}
