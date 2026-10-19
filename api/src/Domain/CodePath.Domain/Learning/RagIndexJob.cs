namespace CodePath.Domain.Learning;

// Transactional outbox: committed together with the Topic mutation.
public sealed class RagIndexJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TopicId { get; set; }
    public Guid Revision { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public int ChunkCount { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Never indexed: this record may contain answer keys and a complete reference solution.
public sealed class RagTeachingDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TopicId { get; set; }
    public Guid SourceRevision { get; set; }
    public Guid CreatedBy { get; set; }
    public string Kind { get; set; } = "exercise";
    public string Language { get; set; } = "python";
    public string Content { get; set; } = "{}";
    public string Sources { get; set; } = "[]";
    public string Status { get; set; } = "Draft";
    public string Validation { get; set; } = "NotRun";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
