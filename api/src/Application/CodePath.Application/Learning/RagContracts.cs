using CodePath.Application.Judge;
namespace CodePath.Application.Learning;

public sealed record RagScope(Guid TopicId, Guid Revision);
public sealed record RagTopic(Guid Id, Guid ClassId, Guid Revision, int Version, string Title, string Slug, string Content);
public sealed record RagPassage(Guid ChunkId, Guid DocumentId, Guid BlockId, Guid TopicId, Guid ClassId,
    Guid Revision, int Version, string Title, int? Page, string Heading, string Content, double Score);
public sealed record RagReference(Guid ChunkId, Guid DocumentId, Guid BlockId, Guid TopicId, Guid ClassId,
    int Version, string Title, int? Page, string Heading);
public sealed record RagSearchResult(RagPassage[] Passages, string Collection);
public sealed record RagAnswer(string Answer, string[] SourceIds, bool InsufficientContext);
public sealed record RagAskInput(string Question);
public sealed record RagAskResult(string Answer, RagReference[] Citations, string GroundingStatus);
public interface ILearningRagAccess
{
    Task<object> RagClasses(Actor actor, CancellationToken ct);
    Task<RagTopic> RagTopic(Guid id, Actor actor, bool manage, CancellationToken ct);
    Task<Guid[]> RagProblemTopics(Guid problemId, Actor actor, CancellationToken ct);
}
public interface IRagClient
{
    Task<int> Index(RagTopic topic, object[] documents, CancellationToken ct);
    Task Delete(RagScope scope, CancellationToken ct);
    Task<RagSearchResult> Search(string query, RagScope[] scopes, CancellationToken ct);
    Task<RagAnswer> Answer(string question, RagPassage[] passages, CancellationToken ct);
    Task<TeachingDraftContent> Draft(string question, string kind, string language, RagPassage[] passages, CancellationToken ct);
}
public interface IRagService
{
    Task<object> Status(Guid topicId, Actor actor, CancellationToken ct);
    Task<object> Reindex(Guid topicId, Actor actor, CancellationToken ct);
    Task<RagPassage[]> ForProblem(Guid problemId, string query, Actor actor, CancellationToken ct);
    Task<RagAskResult> Ask(Guid topicId, string question, Actor actor, CancellationToken ct);
    Task<RagPassage[]> Search(Guid topicId, string question, Actor actor, CancellationToken ct);
    Task<bool> StillAllowed(RagPassage[] passages, Actor actor, CancellationToken ct);
    Task<object> Source(Guid topicId, Guid blockId, int version, Actor actor, CancellationToken ct);
    Task<object> GenerateDraft(Guid topicId, RagDraftInput input, Actor actor, CancellationToken ct);
    Task<object> Drafts(Guid topicId, Actor actor, CancellationToken ct);
    Task<object> EditDraft(Guid topicId, Guid draftId, RagDraftEdit input, Actor actor, CancellationToken ct);
    Task<object> VerifyDraft(Guid topicId, Guid draftId, Actor actor, CancellationToken ct);
    Task<object> ReviewDraft(Guid topicId, Guid draftId, Guid rowVersion, Actor actor, CancellationToken ct);
}
public sealed record RagDraftInput(string Question, string Kind = "exercise", string Language = "python");
public sealed record DraftTest(string Input, string ExpectedOutput, bool IsSample);
public sealed record DraftQuizItem(string Question, string[] Choices, int CorrectIndex, string Explanation);
public sealed record TeachingDraftContent(string Title, string Description, string ReferenceSolution,
    DraftTest[] Tests, DraftQuizItem[] Quiz, string[] SourceIds);
public sealed record RagDraftEdit(Guid RowVersion, TeachingDraftContent Content);
public sealed record RagDraftReview(Guid RowVersion);
