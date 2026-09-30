using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Auditing.Entities;

public sealed class AuditLog : BaseEntity
{
    public Guid ActorId { get; private set; }
    public AuditAction Action { get; private set; }
    public Guid TargetUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string DataJson { get; private set; } = "{}";

    private AuditLog() { }

    public static AuditLog Create(
        Guid actorId,
        AuditAction action,
        Guid targetUserId,
        string? ipAddress,
        string dataJson,
        DateTime utcNow)
    {
        if (actorId == Guid.Empty)
            throw new DomainValidationException("Actor ID cannot be empty.", nameof(actorId));
        if (targetUserId == Guid.Empty)
            throw new DomainValidationException("Target user ID cannot be empty.", nameof(targetUserId));
        if (string.IsNullOrWhiteSpace(dataJson))
            throw new DomainValidationException("Audit data cannot be empty.", nameof(dataJson));

        EnsureUtc(utcNow);
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetUserId = targetUserId,
            OccurredAt = utcNow,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            DataJson = dataJson,
            CreatedAt = utcNow,
            CreatedBy = actorId.ToString()
        };
    }
}
