using CodePath.Application.Judge;
using CodePath.Domain.Learning;
using CodePath.Shared.Kernel.Common;
namespace CodePath.Application.Learning;
public sealed record CommentInput(string Body, Guid? ParentId = null);
public sealed record NoteInput(string Body, Guid? BlockId = null);
public sealed record AdjustmentInput(Guid UserId, decimal Points, string Reason, Guid IdempotencyKey);
public interface ILearningExtras
{
    Task<Result<object>> Search(Guid classId, string query, int page, Actor actor, CancellationToken ct);
    Task<Result<object>> Scores(Guid classId, string action, object? input, Actor actor, CancellationToken ct);
    Task<Result<object>> Extend(Guid topicId, ExtensionInput input, Actor actor, CancellationToken ct);
    Task<Result<object>> Clone(Guid targetId, CloneInput input, string key, Actor actor, CancellationToken ct);
    Task<Result<object>> Upstream(Guid topicId, Actor actor, CancellationToken ct);
    Task<Result<object>> Comments(Guid topicId, Guid? commentId, string action, CommentInput? input, int page, Actor actor, CancellationToken ct);
    Task<Result<object>> Notes(Guid topicId, string action, NoteInput? input, Actor actor, CancellationToken ct);
    Task<Result<byte[]>> Export(Guid classId, Actor actor, CancellationToken ct);
}
