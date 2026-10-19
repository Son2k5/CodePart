namespace CodePath.Domain.Judge;

public enum Verdict { Accepted, WrongAnswer, TimeLimitExceeded, MemoryLimitExceeded, RuntimeError, CompileError, InternalError, Pending, Running }
public enum ProblemStatus { Draft, Published }
public enum ComparisonMode { Lines, Tokens }

public sealed class Problem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = "Easy";
    public int TimeLimitMs { get; set; } = 2000;
    public int MemoryLimitMb { get; set; } = 256;
    public int Points { get; set; } = 100;
    public ComparisonMode ComparisonMode { get; set; }
    public ProblemStatus Status { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ClassId { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ProblemStarterCode> StarterCodes { get; set; } = [];
    public List<TestCase> TestCases { get; set; } = [];
    public bool CanPublish => TestCases.Count is >= 10 and <= 15 && TestCases.Count(t => t.IsSample) is >= 1 and <= 2;
}
public sealed class ProblemStarterCode
{
    public Guid ProblemId { get; set; }
    public string Language { get; set; } = "";
    public string Template { get; set; } = "";
}
public sealed class TestCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProblemId { get; set; }
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public bool IsSample { get; set; }
    public int Order { get; set; }
    public int Weight { get; set; } = 1;
}
