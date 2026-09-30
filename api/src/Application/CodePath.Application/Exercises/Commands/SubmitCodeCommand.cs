using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Exercises.Dtos;
using CodePath.Domain.Exercises.Entities;
using CodePath.Domain.Exercises.Enums;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Application.Exercises.Commands;

public sealed record SubmitCodeCommand(
    Guid StudentId,
    Guid ExerciseId,
    string Language,
    string SourceCode) : IRequest<Result<ExecutionResultDto>>;

public sealed class SubmitCodeCommandValidator : AbstractValidator<SubmitCodeCommand>
{
    public SubmitCodeCommandValidator()
    {
        RuleFor(command => command.StudentId).NotEmpty();
        RuleFor(command => command.ExerciseId).NotEmpty();
        RuleFor(command => command.Language).NotEmpty().MaximumLength(32);
        RuleFor(command => command.SourceCode).NotEmpty().MaximumLength(100_000);
    }
}

internal sealed class SubmitCodeCommandHandler(
    IExerciseDbContext dbContext,
    ICodeExecutionService runner,
    TimeProvider timeProvider) : IRequestHandler<SubmitCodeCommand, Result<ExecutionResultDto>>
{
    public async Task<Result<ExecutionResultDto>> Handle(SubmitCodeCommand request, CancellationToken cancellationToken)
    {
        if (!runner.Supports(request.Language))
            return Result<ExecutionResultDto>.Failure("The selected language is not supported.");

        var exercise = await dbContext.GetPublishedByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise is null)
            return Result<ExecutionResultDto>.Failure("Exercise was not found.", ErrorCodes.NotFound);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var submission = Submission.Create(exercise.Id, request.StudentId, request.Language, request.SourceCode, utcNow);
        var domainResults = new List<SubmissionTestResult>();
        var responseResults = new List<TestCaseResultDto>();

        foreach (var test in exercise.TestCases.OrderBy(test => test.Order))
        {
            var outcome = await runner.ExecuteAsync(
                request.Language, request.SourceCode, test.Input, test.ExpectedOutput,
                exercise.TimeLimitMs, exercise.MemoryLimitKb, cancellationToken);

            domainResults.Add(SubmissionTestResult.Create(
                submission.Id, test.Id, outcome.Status, outcome.StandardOutput, outcome.ErrorMessage,
                outcome.RuntimeMs, outcome.MemoryKb, utcNow));

            responseResults.Add(new TestCaseResultDto(
                test.Id, test.Order, outcome.Status.ToString(),
                test.IsSample ? test.Input : null,
                test.IsSample ? test.ExpectedOutput : null,
                test.IsSample ? outcome.StandardOutput : null,
                outcome.ErrorMessage,
                outcome.RuntimeMs,
                outcome.MemoryKb));

            if (outcome.Status is SubmissionStatus.CompilationError or SubmissionStatus.SystemError)
                break;
        }

        var finalStatus = domainResults.FirstOrDefault(result => result.Status != SubmissionStatus.Accepted)?.Status
            ?? SubmissionStatus.Accepted;
        var runtime = domainResults.Where(result => result.RuntimeMs.HasValue).Sum(result => result.RuntimeMs);
        var memory = domainResults.Where(result => result.MemoryKb.HasValue).Max(result => result.MemoryKb);
        var firstError = domainResults.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.ErrorMessage))?.ErrorMessage;

        submission.Complete(finalStatus, domainResults, exercise.TestCases.Count, runtime, memory, firstError, utcNow);
        await dbContext.AddSubmissionAsync(submission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ExecutionResultDto>.Success(new ExecutionResultDto(
            submission.Id,
            finalStatus.ToString(),
            domainResults.Count(result => result.Status == SubmissionStatus.Accepted),
            exercise.TestCases.Count,
            runtime,
            memory,
            responseResults));
    }
}
