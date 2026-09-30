using CodePath.Domain.Exercises.Enums;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Exercises.Entities;

public sealed class SubmissionTestResult : BaseEntity
{
    public Guid SubmissionId { get; private set; }
    public Submission Submission { get; private set; } = default!;
    public Guid TestCaseId { get; private set; }
    public ExerciseTestCase TestCase { get; private set; } = default!;
    public SubmissionStatus Status { get; private set; }
    public string? ActualOutput { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? RuntimeMs { get; private set; }
    public int? MemoryKb { get; private set; }

    private SubmissionTestResult() { }

    public static SubmissionTestResult Create(
        Guid submissionId,
        Guid testCaseId,
        SubmissionStatus status,
        string? actualOutput,
        string? errorMessage,
        int? runtimeMs,
        int? memoryKb,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        return new SubmissionTestResult
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            TestCaseId = testCaseId,
            Status = status,
            ActualOutput = actualOutput,
            ErrorMessage = errorMessage,
            RuntimeMs = runtimeMs,
            MemoryKb = memoryKb,
            CreatedAt = utcNow
        };
    }
}
