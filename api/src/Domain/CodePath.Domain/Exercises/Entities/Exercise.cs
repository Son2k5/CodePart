using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Exercises.Entities;

public sealed class Exercise : BaseEntity
{
    public string Slug { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Difficulty { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Constraints { get; private set; } = default!;
    public string StarterCode { get; private set; } = default!;
    public int TimeLimitMs { get; private set; }
    public int MemoryLimitKb { get; private set; }
    public bool IsPublished { get; private set; }

    public ICollection<ExerciseTestCase> TestCases { get; private set; } = new List<ExerciseTestCase>();
    public ICollection<Submission> Submissions { get; private set; } = new List<Submission>();

    private Exercise() { }

    public static Exercise Create(
        string slug,
        string title,
        string difficulty,
        string description,
        string constraints,
        string starterCode,
        int timeLimitMs,
        int memoryLimitKb,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("Slug and title are required.");
        if (timeLimitMs is < 100 or > 15_000)
            throw new DomainValidationException("Time limit must be between 100 and 15000 ms.");
        if (memoryLimitKb is < 16_384 or > 1_048_576)
            throw new DomainValidationException("Memory limit must be between 16 MB and 1 GB.");

        EnsureUtc(utcNow);
        return new Exercise
        {
            Id = Guid.NewGuid(),
            Slug = slug.Trim().ToLowerInvariant(),
            Title = title.Trim(),
            Difficulty = difficulty.Trim(),
            Description = description.Trim(),
            Constraints = constraints.Trim(),
            StarterCode = starterCode,
            TimeLimitMs = timeLimitMs,
            MemoryLimitKb = memoryLimitKb,
            CreatedAt = utcNow
        };
    }

    public void Publish(DateTime utcNow)
    {
        if (TestCases.Count == 0)
            throw new DomainRuleViolationException("An exercise needs at least one test case before publishing.");

        IsPublished = true;
        Touch(utcNow);
    }

    public void AddTestCase(
        string input,
        string expectedOutput,
        bool isSample,
        int order,
        string? explanation,
        DateTime utcNow)
    {
        if (TestCases.Any(test => test.Order == order))
            throw new DomainRuleViolationException("Test case order must be unique inside an exercise.");

        TestCases.Add(ExerciseTestCase.Create(
            Id, input, expectedOutput, isSample, order, explanation, utcNow));
    }
}
