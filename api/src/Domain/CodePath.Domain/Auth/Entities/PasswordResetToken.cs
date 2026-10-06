using CodePath.Shared.Kernel.Entities;

namespace CodePath.Domain.Auth.Entities;

public sealed class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public string? RequestedByIp { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime utcNow,
        string? requestedByIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        EnsureUtc(utcNow);
        EnsureUtc(expiresAtUtc);

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (expiresAtUtc <= utcNow)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));

        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc,
            RequestedByIp = requestedByIp,
            CreatedAt = utcNow
        };
    }
}
