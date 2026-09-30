using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Exercises.Dtos;
using CodePath.Shared.Kernel.Common;
using MediatR;

namespace CodePath.Application.Exercises.Queries;

public sealed record GetExerciseQuery(string Slug) : IRequest<Result<ExerciseDto>>;

internal sealed class GetExerciseQueryHandler(IExerciseDbContext dbContext)
    : IRequestHandler<GetExerciseQuery, Result<ExerciseDto>>
{
    private static readonly string[] Languages = ["python", "javascript", "java", "csharp", "cpp"];

    public async Task<Result<ExerciseDto>> Handle(GetExerciseQuery request, CancellationToken cancellationToken)
    {
        var exercise = await dbContext.GetPublishedBySlugAsync(request.Slug.Trim().ToLowerInvariant(), cancellationToken);
        if (exercise is null)
            return Result<ExerciseDto>.Failure("Exercise was not found.", ErrorCodes.NotFound);

        var samples = exercise.TestCases
            .Where(test => test.IsSample)
            .OrderBy(test => test.Order)
            .Select(test => new SampleTestCaseDto(test.Id, test.Input, test.ExpectedOutput, test.Explanation))
            .ToArray();

        return Result<ExerciseDto>.Success(new ExerciseDto(
            exercise.Id,
            exercise.Slug,
            exercise.Title,
            exercise.Difficulty,
            exercise.Description,
            exercise.Constraints,
            exercise.StarterCode,
            exercise.TimeLimitMs,
            exercise.MemoryLimitKb,
            Languages,
            samples));
    }
}
