using System.Data;
using System.Security.Cryptography;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Notifications.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auditing.Enums;
using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Auth.Services.Tokens;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext : DbContext, IAuthDbContext, IUsersDbContext, IAdminDbContext, INotificationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Auth Module Entities
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Admin/Audit Module Entities
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Users Module Entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Class> Classes => Set<Class>();

    // IAuthDbContext Implementation
    Task<RefreshToken?> IAuthDbContext.GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
        => RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    Task IAuthDbContext.AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
        => RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    async Task<bool> IAuthDbContext.TryRotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacement,
        DateTime revokedAtUtc,
        string? revokedByIp,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryingTransactionAsync(async transactionCancellationToken =>
        {
            var affectedRows = await RefreshTokens
                .Where(token => token.TokenHash == currentTokenHash
                    && token.RevokedAt == null
                    && token.ExpiresAt > revokedAtUtc
                    && token.AbsoluteExpiresAt > revokedAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, revokedAtUtc)
                    .SetProperty(token => token.RevokedByIp, revokedByIp)
                    .SetProperty(token => token.RevokedReason, "ROTATED")
                    .SetProperty(token => token.ReplacedByTokenHash, replacement.TokenHash)
                    .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                    .SetProperty(token => token.UpdatedBy, revokedByIp),
                    transactionCancellationToken);

            if (affectedRows != 1)
                return false;

            // The update above holds the parent row lock until commit. Preserve any
            // activity committed before rotation instead of copying a stale read.
            var activity = await RefreshTokens.Where(t => t.TokenHash == currentTokenHash)
                .Select(t => t.LastActiveAt).SingleAsync(transactionCancellationToken);
            replacement.PreserveActivity(activity);
            await RefreshTokens.AddAsync(replacement, transactionCancellationToken);
            await base.SaveChangesAsync(transactionCancellationToken);
            return true;
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    Task<int> IAuthDbContext.RevokeTokenFamilyAsync(
        Guid familyId,
        DateTime revokedAtUtc,
        string? revokedByIp,
        CancellationToken cancellationToken)
        => RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAtUtc)
                .SetProperty(token => token.RevokedByIp, revokedByIp)
                .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                .SetProperty(token => token.UpdatedBy, revokedByIp),
                cancellationToken);

    Task<int> IAuthDbContext.RevokeRefreshTokenAsync(
        string tokenHash,
        DateTime revokedAtUtc,
        string? revokedByIp,
        CancellationToken cancellationToken)
        => RefreshTokens
            .Where(token => token.TokenHash == tokenHash && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAtUtc)
                .SetProperty(token => token.RevokedByIp, revokedByIp)
                .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                .SetProperty(token => token.UpdatedBy, revokedByIp),
                cancellationToken);

    Task<int> IAuthDbContext.RevokeAllUserRefreshTokensAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string? revokedByIp,
        CancellationToken cancellationToken)
        => RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAtUtc)
                .SetProperty(token => token.RevokedByIp, revokedByIp)
                .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                .SetProperty(token => token.UpdatedBy, revokedByIp),
                cancellationToken);

    async Task<bool> IAuthDbContext.RevokeAllUserSessionsAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string? revokedByIp,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryingTransactionAsync(async transactionCancellationToken =>
        {
            var affectedUsers = await Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.SecurityVersion, user => user.SecurityVersion + 1)
                    .SetProperty(user => user.UpdatedAt, revokedAtUtc)
                    .SetProperty(user => user.UpdatedBy, revokedByIp),
                    transactionCancellationToken);

            if (affectedUsers != 1)
                return false;

            await RefreshTokens
                .Where(token => token.UserId == userId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, revokedAtUtc)
                    .SetProperty(token => token.RevokedByIp, revokedByIp)
                    .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                    .SetProperty(token => token.UpdatedBy, revokedByIp),
                    transactionCancellationToken);

            return true;
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    async Task<PasswordResetCompletion?> IAuthDbContext.TryResetPasswordAsync(
        Guid userId,
        string expectedFingerprint,
        string newPasswordHash,
        DateTime utcNow,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryingTransactionAsync(async transactionCancellationToken =>
        {
            var user = await Users.FirstOrDefaultAsync(existing => existing.Id == userId, transactionCancellationToken);
            if (user is null)
                return null;

            // Stateless token replay guard: the token carries the fingerprint of the hash at issue time.
            if (!PasswordFingerprint.Matches(user.PasswordHash, expectedFingerprint))
                return null;

            user.ChangePassword(newPasswordHash, utcNow);
            user.IncrementSecurityVersion(utcNow);
            AuditLogs.Add(AuditLog.Create(userId, AuditAction.PasswordReset, userId, ipAddress,
                "{\"source\":\"stateless-reset\"}", utcNow));

            await RefreshTokens
                .Where(refreshToken => refreshToken.UserId == userId && refreshToken.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(refreshToken => refreshToken.RevokedAt, utcNow)
                    .SetProperty(refreshToken => refreshToken.RevokedByIp, ipAddress)
                    .SetProperty(refreshToken => refreshToken.UpdatedAt, utcNow)
                    .SetProperty(refreshToken => refreshToken.UpdatedBy, ipAddress),
                    transactionCancellationToken);

            await base.SaveChangesAsync(transactionCancellationToken);
            return new PasswordResetCompletion(user.Id, user.Email);
        }, IsolationLevel.ReadCommitted, cancellationToken);
    }

    Task<int> IAuthDbContext.DeleteExpiredRefreshTokensAsync(
        DateTime expiredBeforeUtc,
        CancellationToken cancellationToken)
        => RefreshTokens
            .Where(token => token.ExpiresAt < expiredBeforeUtc)
            .ExecuteDeleteAsync(cancellationToken);

    // IUsersDbContext Implementation
    Task<User?> IUsersDbContext.GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    Task<User?> IUsersDbContext.GetByEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    Task<User?> IUsersDbContext.GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        => Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    Task<User?> IUsersDbContext.GetByIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken)
        => Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    Task IUsersDbContext.AddAsync(User user, CancellationToken cancellationToken)
        => Users.AddAsync(user, cancellationToken).AsTask();

    // IAdminDbContext Implementation
    Task<TResult> IAdminDbContext.ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        ExecuteWithRetryingTransactionAsync(operation, IsolationLevel.Serializable, cancellationToken);

    async Task<IReadOnlyList<User>> IAdminDbContext.GetTeachersAsync(
        UserStatus? status,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Teacher);

        if (status.HasValue)
            query = query.Where(user => user.Status == status.Value);

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
        {
            query = query.Where(user =>
                user.CreatedAt < cursorCreatedAt.Value
                || (user.CreatedAt == cursorCreatedAt.Value && user.Id.CompareTo(cursorId.Value) < 0));
        }

        return await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    async Task<IReadOnlyList<AuditLog>> IAdminDbContext.GetAuditLogsAsync(
        Guid? targetUserId,
        DateTime? cursorOccurredAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = AuditLogs.AsNoTracking().AsQueryable();

        if (targetUserId.HasValue)
            query = query.Where(log => log.TargetUserId == targetUserId.Value);

        if (cursorOccurredAt.HasValue && cursorId.HasValue)
        {
            query = query.Where(log =>
                log.OccurredAt < cursorOccurredAt.Value
                || (log.OccurredAt == cursorOccurredAt.Value && log.Id.CompareTo(cursorId.Value) < 0));
        }

        return await query
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    Task IAdminDbContext.AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        => AuditLogs.AddAsync(auditLog, cancellationToken).AsTask();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnqueueRagChanges();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            throw new ConflictException("Email, Student ID, or unique record already exists.");
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Modified))
            entry.Property(u => u.Version).CurrentValue = checked(entry.Property(u => u.Version).OriginalValue + 1);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private async Task<TResult> ExecuteWithRetryingTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async strategyCancellationToken =>
        {
            ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(
                isolationLevel,
                strategyCancellationToken);
            var result = await operation(strategyCancellationToken);
            await transaction.CommitAsync(strategyCancellationToken);
            return result;
        }, cancellationToken);
    }
}
