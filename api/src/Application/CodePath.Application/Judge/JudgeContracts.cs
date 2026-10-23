using CodePath.Domain.Judge;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using System.Text;

namespace CodePath.Application.Judge;

public sealed record Actor(Guid Id, bool IsAdmin, bool IsTeacher);
public sealed record CodeRequest(string Language, string SourceCode, Guid[]? TestCaseIds = null, string? CustomInput = null, Guid? TopicId = null);
public sealed class CodeRequestValidator : AbstractValidator<CodeRequest>
{
    public CodeRequestValidator()
    {
        RuleFor(x => x.Language).NotEmpty().MaximumLength(32);
        RuleFor(x => x.SourceCode).NotEmpty().Must(x => x != null && Encoding.UTF8.GetByteCount(x) <= JudgeRules.SourceBytes);
        RuleFor(x => x.CustomInput).Must(x => x == null || Encoding.UTF8.GetByteCount(x) <= JudgeRules.IoBytes);
        RuleFor(x => x.TestCaseIds).Must(x => x == null || x.Length is >= 1 and <= 2 && x.Distinct().Count() == x.Length);
        RuleFor(x => x).Must(x => x.CustomInput == null || x.TestCaseIds == null);
    }
}
public sealed record ProblemWrite(string Title, string Description, string Difficulty, int TimeLimitMs,
    int MemoryLimitMb, int Points, Guid? CourseId, ComparisonMode ComparisonMode, List<StarterDto> StarterCodes, Guid? ClassId = null, string Category = "general");
public sealed record StarterDto(string Language, string Template);
public sealed record TestWrite(string Input, string ExpectedOutput, bool IsSample, int Order, int Weight);
public sealed class ProblemWriteValidator : AbstractValidator<ProblemWrite>
{
    public ProblemWriteValidator()
    {
        RuleFor(x => x.Category).Must(x => new[] { "general", "basics", "arrays", "strings", "math", "search-sort", "data-structures", "graphs", "dynamic-programming" }.Contains(x));
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(100_000);
        RuleFor(x => x.Difficulty).Must(x => new[] { "Easy", "Medium", "Hard" }.Contains(x));
        RuleFor(x => x.TimeLimitMs).InclusiveBetween(100, 15000);
        RuleFor(x => x.MemoryLimitMb).InclusiveBetween(16, 1024);
        RuleFor(x => x.Points).InclusiveBetween(0, 10000);
        RuleFor(x => x.ComparisonMode).IsInEnum();
        RuleFor(x => x).Must(x => !(x.CourseId.HasValue && x.ClassId.HasValue))
            .WithMessage("Choose at most one class or course; leave both empty for the practice library.");
        RuleFor(x => x.CourseId).NotEqual(Guid.Empty).When(x => x.CourseId.HasValue);
        RuleFor(x => x.ClassId).NotEqual(Guid.Empty).When(x => x.ClassId.HasValue);
        RuleFor(x => x.StarterCodes).NotNull().Must(x => x is { Count: > 0 and <= 20 } && x.All(s => s != null && !string.IsNullOrEmpty(s.Language) && s.Template != null && Encoding.UTF8.GetByteCount(s.Template) <= JudgeRules.SourceBytes) && x.Select(s => s.Language).Distinct().Count() == x.Count);
    }
}
public sealed class TestWriteValidator : AbstractValidator<TestWrite>
{
    public TestWriteValidator()
    {
        RuleFor(x => x.Input).NotNull().Must(x => x != null && Encoding.UTF8.GetByteCount(x) <= JudgeRules.IoBytes);
        RuleFor(x => x.ExpectedOutput).NotNull().Must(x => x != null && Encoding.UTF8.GetByteCount(x) <= JudgeRules.IoBytes);
        RuleFor(x => x.Order).InclusiveBetween(1, 15);
        RuleFor(x => x.Weight).InclusiveBetween(1, 1000);
    }
}
public sealed record Limits(int TimeLimitMs, int MemoryLimitMb);
public sealed record ExecutionResult(Verdict Verdict, string? Output, string? Error, int RuntimeMs, int MemoryKb);
public interface ICodeExecutor
{
    bool Supports(string language);
    Task<ExecutionResult> Execute(string language, string sourceCode, string stdin, Limits limits, CancellationToken ct);
}
public sealed record TestDto(Guid? TestCaseId, int Index, bool IsSample, string Verdict, int RuntimeMs,
    int? MemoryKb = null, string? Input = null, string? ExpectedOutput = null, string? ActualOutput = null, string? ErrorMessage = null);
public sealed record SubmissionDto(Guid Id, Guid ProblemId, string Language, string Status, int PassedCount, int TotalCount,
    decimal Score, int AwardedPoints, int MaxRuntimeMs, int MaxMemoryKb, string? CompileOutput,
    DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<TestDto> Results, string? SourceCode = null);
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
public sealed record FailureNotification(SubmissionFailedEvent Event) : MediatR.INotification;
public sealed record JudgeScope(Guid Id, string Name, string Kind);
public interface IJudgeAccess
{
    Task<IReadOnlyList<JudgeScope>> Scopes(Actor actor, bool manage, CancellationToken ct);
    Task<bool> CanAccess(Guid? courseId, Guid? classId, Actor actor, bool manage, CancellationToken ct);
}

public interface IJudgeService
{
    Task<Result<object>> GetProblem(Guid id, Actor actor, bool manage, CancellationToken ct);
    Task<Result<object>> ListProblems(Actor actor, bool manage, int page, CancellationToken ct, Guid? courseId = null, Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null);
    Task<Result<object>> SaveProblem(Guid? id, ProblemWrite request, Actor actor, CancellationToken ct);
    Task<Result<object>> ChangeProblem(Guid id, string action, Actor actor, CancellationToken ct);
    Task<Result<object>> SaveTests(Guid id, List<TestWrite> tests, Actor actor, CancellationToken ct);
    Task<Result<object>> Run(Guid id, CodeRequest request, Actor actor, CancellationToken ct);
    Task<Result<object>> Submit(Guid id, CodeRequest request, Actor actor, CancellationToken ct);
    Task<Result<SubmissionDto>> GetSubmission(Guid id, Actor actor, bool manage, CancellationToken ct);
    Task<Result<Page<SubmissionDto>>> History(Actor actor, bool manage, Guid? problem, Guid? course, Guid? user, Verdict? verdict, int page, int size, CancellationToken ct, Guid? classId = null, bool library = false, string? search = null, string? difficulty = null, string? category = null, string? language = null);
}
