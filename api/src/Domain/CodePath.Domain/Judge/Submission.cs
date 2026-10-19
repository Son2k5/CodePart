namespace CodePath.Domain.Judge;

public sealed class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProblemId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? TopicId { get; set; }
    public DateTime? LearningProcessedAt { get; set; }
    public string Language { get; set; } = "";
    public string SourceCode { get; set; } = "";
    public Verdict Status { get; set; } = Verdict.Pending;
    public int PassedCount { get; set; }
    public int TotalCount { get; set; }
    public decimal Score { get; set; }
    public int AwardedPoints { get; set; }
    public int MaxRuntimeMs { get; set; }
    public int MaxMemoryKb { get; set; }
    public string? CompileOutput { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public int Attempts { get; set; }
    // Immutable judging snapshot: editing a problem cannot change an in-flight job.
    public string Snapshot { get; set; } = "";
    public List<SubmissionResult> Results { get; set; } = [];
}
public sealed class SubmissionResult
{
    public Guid SubmissionId { get; set; }
    public Guid TestCaseId { get; set; }
    public int Index { get; set; }
    public bool IsSample { get; set; }
    public string? Input { get; set; }
    public string? ExpectedOutput { get; set; }
    public Verdict Verdict { get; set; }
    public int RuntimeMs { get; set; }
    public int MemoryKb { get; set; }
    public string? ActualOutput { get; set; }
    public string? ErrorMessage { get; set; }
}
// The composite key is the points ledger: one award per student/problem forever.
public sealed class ProblemAward
{
    public Guid UserId { get; set; }
    public Guid ProblemId { get; set; }
    public Guid SubmissionId { get; set; }
    public int Points { get; set; }
}
public sealed record SubmissionFailedEvent(Guid SubmissionId, Guid FirstFailedTestCaseId, Verdict Verdict) : CodePath.Shared.Kernel.Events.IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
