using CodePath.Domain.Exercises.Enums;

namespace CodePath.Application.Exercises.Abstractions;

public interface ICodeExecutionService
{
    bool Supports(string language);

    Task<CodeExecutionOutcome> ExecuteAsync(
        string language,
        string sourceCode,
        string standardInput,
        string? expectedOutput,
        int timeLimitMs,
        int memoryLimitKb,
        CancellationToken cancellationToken);
}

public sealed record CodeExecutionOutcome(
    SubmissionStatus Status,
    string? StandardOutput,
    string? ErrorMessage,
    int? RuntimeMs,
    int? MemoryKb);
