namespace CodePath.Application.Exercises.Dtos;

public sealed record ExerciseDto(
    Guid Id,
    string Slug,
    string Title,
    string Difficulty,
    string Description,
    string Constraints,
    string StarterCode,
    int TimeLimitMs,
    int MemoryLimitKb,
    IReadOnlyList<string> SupportedLanguages,
    IReadOnlyList<SampleTestCaseDto> SampleTestCases);

public sealed record SampleTestCaseDto(Guid Id, string Input, string ExpectedOutput, string? Explanation);

public sealed record TestCaseResultDto(
    Guid? TestCaseId,
    int Order,
    string Status,
    string? Input,
    string? ExpectedOutput,
    string? ActualOutput,
    string? ErrorMessage,
    int? RuntimeMs,
    int? MemoryKb);

public sealed record ExecutionResultDto(
    Guid? SubmissionId,
    string Status,
    int PassedTests,
    int TotalTests,
    int? RuntimeMs,
    int? MemoryKb,
    IReadOnlyList<TestCaseResultDto> TestCases);
