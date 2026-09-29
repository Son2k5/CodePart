using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Auth.Entities;

public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Grace period cho phép retry sau khi token vừa bị rotate (chống race khi client gửi song song 2 request).
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdByIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static RefreshToken CreateWithDefaultLifetime(Guid userId, string tokenHash, string? createdByIp = null)
        => Create(userId, tokenHash, DateTime.UtcNow.Add(DefaultLifetime), createdByIp);

    /// <summary>
    /// True nếu token vừa bị revoke trong grace period và có replacement → cho phép retry (chống race parallel request).
    /// </summary>
    public bool IsWithinReuseGracePeriod()
        => IsRevoked
        && RevokedAt.HasValue
        && (DateTime.UtcNow - RevokedAt.Value) <= ReuseGracePeriod
        && !string.IsNullOrWhiteSpace(ReplacedByTokenHash);

    public void Revoke(string? revokedByIp = null, string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch(revokedByIp);
    }
}
