using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Exercises.Dtos;
using CodePath.Domain.Exercises.Enums;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Exercises.Commands;

public sealed record RunCodeCommand(
    Guid ExerciseId,
    string Language,
    string SourceCode,
    string? CustomInput) : IRequest<Result<ExecutionResultDto>>;

public sealed class RunCodeCommandValidator : AbstractValidator<RunCodeCommand>
{
    public RunCodeCommandValidator()
    {
        RuleFor(command => command.ExerciseId).NotEmpty();
        RuleFor(command => command.Language).NotEmpty().MaximumLength(32);
        RuleFor(command => command.SourceCode).NotEmpty().MaximumLength(100_000);
        RuleFor(command => command.CustomInput).MaximumLength(20_000);
    }
}

internal sealed class RunCodeCommandHandler(IExerciseDbContext dbContext, ICodeExecutionService runner)
    : IRequestHandler<RunCodeCommand, Result<ExecutionResultDto>>
{
    public async Task<Result<ExecutionResultDto>> Handle(RunCodeCommand request, CancellationToken cancellationToken)
    {
        if (!runner.Supports(request.Language))
            return Result<ExecutionResultDto>.Failure("The selected language is not supported.");

        var exercise = await dbContext.GetPublishedByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise is null)
            return Result<ExecutionResultDto>.Failure("Exercise was not found.", ErrorCodes.NotFound);

        var cases = request.CustomInput is null
            ? exercise.TestCases.Where(test => test.IsSample).OrderBy(test => test.Order)
                .Select(test => (Id: (Guid?)test.Id, test.Order, test.Input, Expected: (string?)test.ExpectedOutput)).ToArray()
            : [(Id: (Guid?)null, Order: 1, Input: request.CustomInput, Expected: (string?)null)];

        var results = new List<TestCaseResultDto>(cases.Length);
        foreach (var test in cases)
        {
            var outcome = await runner.ExecuteAsync(
                request.Language, request.SourceCode, test.Input, test.Expected,
                exercise.TimeLimitMs, exercise.MemoryLimitKb, cancellationToken);

            results.Add(new TestCaseResultDto(
                test.Id, test.Order, outcome.Status.ToString(), test.Input, test.Expected,
                outcome.StandardOutput, outcome.ErrorMessage, outcome.RuntimeMs, outcome.MemoryKb));

            if (outcome.Status is SubmissionStatus.CompilationError or SubmissionStatus.SystemError)
                break;
        }

        var passed = results.Count(result => result.Status == SubmissionStatus.Accepted.ToString());
        var overall = results.FirstOrDefault(result => result.Status != SubmissionStatus.Accepted.ToString())?.Status
            ?? SubmissionStatus.Accepted.ToString();

        return Result<ExecutionResultDto>.Success(new ExecutionResultDto(
            null, overall, passed, results.Count,
            results.Where(result => result.RuntimeMs.HasValue).Sum(result => result.RuntimeMs),
            results.Where(result => result.MemoryKb.HasValue).Max(result => result.MemoryKb),
            results));
    }
}
