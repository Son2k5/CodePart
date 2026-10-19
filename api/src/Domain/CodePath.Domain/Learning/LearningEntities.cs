using System.Text.Json;
namespace CodePath.Domain.Learning;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TopicStatus>))]
public enum TopicStatus { Draft, Published }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<UnlockRule>))]
public enum UnlockRule { None, AfterPreviousCompleted, AfterDate }
public enum LearningStatus { NotStarted, InProgress, Completed }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AssetKind>))]
public enum AssetKind { Image, Pdf }
public enum AssetStatus { Pending, Ready, Failed }
public sealed class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public Guid? SourceChapterId { get; set; }
    public bool IsDeleted { get; set; }
}
public sealed class Topic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid ChapterId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public int Order { get; set; }
    public int EstimatedMinutes { get; set; }
    public TopicStatus Status { get; set; }
    public DateTime? PublishAt { get; set; }
    public UnlockRule UnlockRule { get; set; }
    public DateTime? UnlockAt { get; set; }
    public string CompletionRule { get; set; } = JsonSerializer.Serialize(new CompletionRule());
    public string DraftContent { get; set; } = "[]";
    public string PublishedContent { get; set; } = "[]";
    public int PublishedVersion { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid? SourceTopicId { get; set; }
    public int? SourcePublishedVersion { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; }
    public void Publish(DateTime now)
    {
        PublishedContent = DraftContent; PublishedVersion++; Status = TopicStatus.Published;
        UpdatedAt = now; RowVersion = Guid.NewGuid();
    }
    public string? LockedReason(DateTime now, bool previousCompleted) =>
        Status != TopicStatus.Published ? "Draft" : PublishAt > now ? "Scheduled" :
        UnlockRule == UnlockRule.AfterDate && (UnlockAt == null || UnlockAt > now) ? "AfterDate" :
        UnlockRule == UnlockRule.AfterPreviousCompleted && !previousCompleted ? "PreviousIncomplete" : null;
}
public sealed record ContentBlock(Guid BlockId, string Type, JsonElement Data);
public sealed record CompletionRule(decimal? RequireVideoPercent = null, decimal? RequireDocumentPercent = null,
    string RequireExercises = "none", bool AllowManualMarkRead = true);
public sealed class TopicExercise
{
    public Guid TopicId { get; set; }
    public Guid ProblemId { get; set; }
    public int Order { get; set; }
    public decimal Points { get; set; }
    public bool IsRequired { get; set; }
    public bool IsDraft { get; set; }
}
public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public AssetKind Kind { get; set; }
    public string StorageKey { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }
    public AssetStatus Status { get; set; }
    public int? PageCount { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
public sealed class TopicProgress
{
    public Guid UserId { get; set; }
    public Guid TopicId { get; set; }
    public Guid ClassId { get; set; }
    public LearningStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool CompletedLate { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public int ExercisePassedCount { get; set; }
    public int ExerciseTotalCount { get; set; }
}
public sealed class BlockProgress
{
    public Guid UserId { get; set; }
    public Guid TopicId { get; set; }
    public Guid BlockId { get; set; }
    public string CoveredRanges { get; set; } = "[]";
    public double DurationSec { get; set; }
    public double? LastPositionSec { get; set; }
    public int? MaxPage { get; set; }
    public int? TotalPages { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ActivityDay { public Guid UserId { get; set; } public DateOnly Date { get; set; } }
public sealed class ClassScoreConfig { public Guid ClassId { get; set; } public string Configuration { get; set; } = "{}"; }
public sealed class StudentDeadlineExtension
{
    public Guid UserId { get; set; } public Guid TopicId { get; set; }
    public DateTime NewDueAt { get; set; } public string Reason { get; set; } = ""; public Guid GrantedBy { get; set; }
}
public sealed class ScoreEntry
{
    public Guid UserId { get; set; } public Guid ClassId { get; set; } public string Source { get; set; } = "Exercise";
    public Guid SourceRefId { get; set; } public Guid TopicId { get; set; } public decimal BasePoints { get; set; }
    public decimal PenaltyPercent { get; set; } public decimal LateByMinutes { get; set; } public decimal EffectivePoints { get; set; }
}
public sealed class TopicSearchIndex
{
    public Guid TopicId { get; set; } public Guid ClassId { get; set; } public string Document { get; set; } = ""; public DateTime UpdatedAt { get; set; }
}
public sealed class Badge { public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string Description { get; set; } = ""; }
public sealed class UserBadge
{
    public Guid UserId { get; set; } public string BadgeCode { get; set; } = "";
    // Guid.Empty represents a global badge; avoids MySQL nullable unique-key duplicates.
    public Guid ClassId { get; set; } public DateTime AwardedAt { get; set; }
}
public sealed class TopicComment
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid TopicId { get; set; } public Guid ClassId { get; set; }
    public Guid UserId { get; set; } public Guid? ParentId { get; set; } public string Body { get; set; } = "";
    public bool IsPinned { get; set; } public bool IsDeleted { get; set; } public DateTime CreatedAt { get; set; } public DateTime? EditedAt { get; set; }
}
public sealed class TopicNote
{
    public Guid UserId { get; set; } public Guid TopicId { get; set; } public Guid? BlockId { get; set; }
    public string Body { get; set; } = ""; public DateTime UpdatedAt { get; set; }
}
