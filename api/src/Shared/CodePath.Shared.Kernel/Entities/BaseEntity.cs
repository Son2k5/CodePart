using System.ComponentModel.DataAnnotations.Schema;
using CodePath.Shared.Kernel.Events;

namespace CodePath.Shared.Kernel.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public string? CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void SetCreatedAudit(string? createdBy = null)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public void Touch(string? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(updatedBy))
        {
            UpdatedBy = updatedBy;
        }
    }
}
