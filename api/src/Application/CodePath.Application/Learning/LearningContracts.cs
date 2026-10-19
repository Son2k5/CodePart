using CodePath.Application.Judge;
using CodePath.Domain.Learning;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;
namespace CodePath.Application.Learning;

public sealed record ChapterInput(string Title, int Order);
public sealed record TopicInput(Guid ChapterId, string Title, string Slug, string Summary, int Order, int EstimatedMinutes,
    UnlockRule UnlockRule = UnlockRule.None, DateTime? UnlockAt = null);
public sealed record DraftInput(Guid RowVersion, ContentBlock[] Blocks);
public sealed record HeartbeatInput(Guid BlockId, double? PositionSec = null, int? Page = null, int? TotalPages = null);
public sealed record ExerciseInput(Guid ProblemId, int Order, decimal Points, bool IsRequired);
public sealed record ReorderInput(Guid[] Ids, Guid? ChapterId = null);
public sealed record ExtensionInput(Guid UserId, DateTime NewDueAt, string Reason);
public sealed record CloneInput(Guid SourceClassId, Guid[]? ChapterIds, Guid[]? TopicIds, bool CopyProblems, Guid? AfterChapterId);
public sealed record SubmissionJudgedEvent(Guid SubmissionId, Guid UserId, Guid TopicId) : INotification;
public sealed record TopicCompleted(Guid UserId, Guid TopicId, Guid ClassId) : INotification;
public sealed record ChapterCompleted(Guid UserId, Guid ChapterId, Guid ClassId) : INotification;
public sealed record ClassCompleted(Guid UserId, Guid ClassId) : INotification;
public interface ILearningService
{
    Task<bool> CanReadLinkedProblem(Guid problemId, Actor actor, CancellationToken ct);
    Task<Result<object>> Outline(Guid classId, Actor actor, CancellationToken ct);
    Task<Result<object>> Read(Guid classId, string slug, Actor actor, CancellationToken ct);
    Task<Result<object>> Chapters(Guid classId, Guid? id, string action, ChapterInput? input, ReorderInput? reorder, Actor actor, CancellationToken ct);
    Task<Result<object>> Topics(Guid classId, Guid? id, string action, TopicInput? input, ReorderInput? reorder, Actor actor, CancellationToken ct);
    Task<Result<object>> Edit(Guid id, string action, object? input, Actor actor, CancellationToken ct);
    Task<Result<object>> Progress(Guid id, HeartbeatInput? heartbeat, Actor actor, CancellationToken ct);
    Task<Result<object>> Report(Guid classId, Guid? student, int page, int pageSize, Actor actor, CancellationToken ct);
    Task<Result<object>> Mode(Guid classId, CodePath.Domain.Users.Entities.ClassMode mode, Actor actor, CancellationToken ct);
    Task<Result<Guid>> SubmissionContext(Guid topicId, Guid problemId, Actor actor, CancellationToken ct);
    Task Judged(SubmissionJudgedEvent notification, CancellationToken ct);
    Task<Result<object>> Personal(string action, Actor actor, CancellationToken ct);
}
public sealed class TopicInputValidator : AbstractValidator<TopicInput>
{
    public TopicInputValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty(); RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(150).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).NotNull().MaximumLength(5000); RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EstimatedMinutes).InclusiveBetween(0, 100000); RuleFor(x => x.UnlockRule).IsInEnum();
        RuleFor(x => x.UnlockAt).NotNull().When(x => x.UnlockRule == UnlockRule.AfterDate);
        RuleFor(x => x.UnlockAt).Must(x => x == null || x.Value.Kind == DateTimeKind.Utc);
    }
}
public sealed class CompletionRuleValidator : AbstractValidator<CompletionRule>
{
    public CompletionRuleValidator()
    {
        RuleFor(x => x.RequireVideoPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.RequireDocumentPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.RequireExercises).Must(x => x is "none" or "all_required" || x != null &&
            x.StartsWith("percent:") && int.TryParse(x[8..], out var n) && n is >= 0 and <= 100);
    }
}
public interface ITopicExerciseDraftGenerator
{
    Task<Result<object>> GenerateAsync(Guid topicId, int count, string difficulty, string[] languages, CancellationToken ct);
}
public sealed class NullTopicExerciseDraftGenerator : ITopicExerciseDraftGenerator
{
    public Task<Result<object>> GenerateAsync(Guid topicId, int count, string difficulty, string[] languages, CancellationToken ct) =>
        Task.FromResult(Result<object>.Failure("AI exercise generation is not enabled.", "FEATURE_NOT_ENABLED"));
}
public sealed record UploadRequest(AssetKind Kind, string MimeType, long SizeBytes);
public sealed record StoredFile(string MimeType, long SizeBytes, byte[] Header, int? PageCount, int? Width, int? Height);
public interface IFileStorage
{
    Task<object> SignUpload(Asset asset, CancellationToken ct);
    Task<StoredFile> Inspect(Asset asset, CancellationToken ct);
    Task<string> SignedUrl(Asset asset, CancellationToken ct);
    Task Delete(Asset asset, CancellationToken ct);
}
public interface ILearningAssets
{
    Task<Result<object>> Sign(UploadRequest request, Actor actor, CancellationToken ct);
    Task<Result<object>> Complete(Guid id, Actor actor, CancellationToken ct);
    Task<Result<object>> Get(Guid id, Actor actor, CancellationToken ct);
}
