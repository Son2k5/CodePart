using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Auth.Entities;

public sealed class RefreshToken : BaseEntity
{
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

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan DefaultAbsoluteLifetime = TimeSpan.FromDays(30);

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        DateTime absoluteExpiresAt,
        DateTime utcNow,
        Guid? familyId = null,
        Guid? parentTokenId = null,
        string? createdByIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        EnsureUtc(utcNow);
        if (absoluteExpiresAt <= utcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteExpiresAt));
        }

        var id = Guid.NewGuid();

        return new RefreshToken
        {
            Id = id,
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

    public static RefreshToken CreateWithDefaultLifetime(
        Guid userId,
        string tokenHash,
        DateTime utcNow,
        string? createdByIp = null)
    {
        return Create(
            userId,
            tokenHash,
            utcNow.Add(DefaultLifetime),
            utcNow.Add(DefaultAbsoluteLifetime),
            utcNow,
            createdByIp: createdByIp);
    }

    public static RefreshToken CreateReplacement(
        RefreshToken parent,
        string tokenHash,
        DateTime utcNow,
        string? createdByIp = null)
        => Create(
            parent.UserId,
            tokenHash,
            utcNow.Add(DefaultLifetime),
            parent.AbsoluteExpiresAt,
            utcNow,
            parent.FamilyId,
            parent.Id,
            createdByIp);

    public void Revoke(DateTime utcNow, string? revokedByIp = null, string? replacedByTokenHash = null)
    {
        EnsureUtc(utcNow);
        RevokedAt = utcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch(utcNow, revokedByIp);
    }
}
