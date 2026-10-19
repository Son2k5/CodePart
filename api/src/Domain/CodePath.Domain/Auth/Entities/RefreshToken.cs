using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Auth.Entities;

public sealed class RefreshToken : BaseEntity
{
    public Guid SessionId { get; private set; }
    public DateTime LastActiveAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid? ParentTokenId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime AbsoluteExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpiredAt(DateTime utcNow) => utcNow >= ExpiresAt;
    public bool IsActiveAt(DateTime utcNow) => !IsRevoked && !IsExpiredAt(utcNow);

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        DateTime absoluteExpiresAt,
        DateTime utcNow,
        Guid? familyId = null,
        Guid? parentTokenId = null,
        string? createdByIp = null,
        Guid? sessionId = null, DateTime? lastActiveAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        EnsureUtc(utcNow);
        EnsureUtc(expiresAt);
        EnsureUtc(absoluteExpiresAt);
        if (lastActiveAt.HasValue) EnsureUtc(lastActiveAt.Value);
        if (absoluteExpiresAt <= utcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteExpiresAt));
        }

        var id = Guid.NewGuid();

        return new RefreshToken
        {
            Id = id,
            SessionId = sessionId ?? familyId ?? id,
            LastActiveAt = lastActiveAt ?? utcNow,
            UserId = userId,
            FamilyId = familyId ?? id,
            ParentTokenId = parentTokenId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt <= absoluteExpiresAt ? expiresAt : absoluteExpiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            CreatedByIp = createdByIp,
            CreatedAt = utcNow
        };
    }

    public static RefreshToken CreateReplacement(
        RefreshToken parent,
        string tokenHash,
        DateTime utcNow,
        TimeSpan lifetime,
        string? createdByIp = null)
        => Create(
            parent.UserId,
            tokenHash,
            utcNow.Add(lifetime),
            parent.AbsoluteExpiresAt,
            utcNow,
            parent.FamilyId,
            parent.Id,
            createdByIp, parent.SessionId, parent.LastActiveAt);

    public void PreserveActivity(DateTime lastActiveAt)
    {
        EnsureUtc(lastActiveAt);
        LastActiveAt = lastActiveAt;
    }

    public void Revoke(DateTime utcNow, string? revokedByIp = null, string? replacedByTokenHash = null)
    {
        EnsureUtc(utcNow);
        RevokedAt = utcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch(utcNow, revokedByIp);
    }
}
