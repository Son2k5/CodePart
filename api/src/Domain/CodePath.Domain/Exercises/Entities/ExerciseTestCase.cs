using CodePath.Shared.Kernel.Entities;
using CodePath.Domain.Common.Exceptions;

namespace CodePath.Domain.Exercises.Entities;

public sealed class ExerciseTestCase : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public Exercise Exercise { get; private set; } = default!;
    public string Input { get; private set; } = default!;
    public string ExpectedOutput { get; private set; } = default!;
    public string? Explanation { get; private set; }
    public bool IsSample { get; private set; }
    public int Order { get; private set; }

    private ExerciseTestCase() { }

    public static ExerciseTestCase Create(
        Guid exerciseId,
        string input,
        string expectedOutput,
        bool isSample,
        int order,
        string? explanation,
        DateTime utcNow)
    {
        if (exerciseId == Guid.Empty)
            throw new DomainValidationException("Exercise ID is required.");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(expectedOutput))
            throw new DomainValidationException("Test input and expected output are required.");
        if (order <= 0)
            throw new DomainValidationException("Test order must be positive.");

        EnsureUtc(utcNow);
        return new ExerciseTestCase
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Input = input,
            ExpectedOutput = expectedOutput,
            Explanation = explanation,
            IsSample = isSample,
            Order = order,
            CreatedAt = utcNow
        };
    }
}
