using CodePath.Domain.Exercises.Enums;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Exercises.Entities;

public sealed class Submission : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public Exercise Exercise { get; private set; } = default!;
    public Guid StudentId { get; private set; }
    public string Language { get; private set; } = default!;
    public string SourceCode { get; private set; } = default!;
    public SubmissionStatus Status { get; private set; }
    public int PassedTests { get; private set; }
    public int TotalTests { get; private set; }
    public int? RuntimeMs { get; private set; }
    public int? MemoryKb { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ICollection<SubmissionTestResult> TestResults { get; private set; } = new List<SubmissionTestResult>();

    private Submission() { }

    public static Submission Create(Guid exerciseId, Guid studentId, string language, string sourceCode, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        return new Submission
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            StudentId = studentId,
            Language = language.Trim().ToLowerInvariant(),
            SourceCode = sourceCode,
            Status = SubmissionStatus.Pending,
            CreatedAt = utcNow,
            CreatedBy = studentId.ToString()
        };
    }

    public void Complete(
        SubmissionStatus status,
        IReadOnlyCollection<SubmissionTestResult> results,
        int totalTests,
        int? runtimeMs,
        int? memoryKb,
        string? errorMessage,
        DateTime utcNow)
    {
        Status = status;
        PassedTests = results.Count(result => result.Status == SubmissionStatus.Accepted);
        TotalTests = totalTests;
        RuntimeMs = runtimeMs;
        MemoryKb = memoryKb;
        ErrorMessage = errorMessage;
        foreach (var result in results)
            TestResults.Add(result);
        Touch(utcNow, StudentId.ToString());
    }
}
