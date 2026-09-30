using CodePath.Shared.Kernel.Events;

namespace CodePath.Shared.Kernel.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void SetCreatedAudit(DateTime utcNow, string? createdBy = null)
    {
        EnsureUtc(utcNow);
        CreatedAt = utcNow;
        CreatedBy = createdBy;
    }

    public void Touch(DateTime utcNow, string? updatedBy = null)
    {
        EnsureUtc(utcNow);
        UpdatedAt = utcNow;
        if (!string.IsNullOrWhiteSpace(updatedBy))
        {
            UpdatedBy = updatedBy;
        }
    }

    protected static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Audit timestamps must use UTC.", nameof(utcNow));
    }
}
