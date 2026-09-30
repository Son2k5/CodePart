using System.Data;
using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Auditing.Entities;
using CodePath.Domain.Auth.Entities;
using CodePath.Domain.Exercises.Entities;
using CodePath.Domain.Users.Entities;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CodePath.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAuthDbContext, IUsersDbContext, IAdminDbContext, IExerciseDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Auth Module Entities
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Admin/Audit Module Entities
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Exercises / Online Judge Module Entities
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseTestCase> ExerciseTestCases => Set<ExerciseTestCase>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionTestResult> SubmissionTestResults => Set<SubmissionTestResult>();

    // Users Module Entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

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
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        var affectedRows = await RefreshTokens
            .Where(token => token.TokenHash == currentTokenHash
                && token.RevokedAt == null
                && token.ExpiresAt > revokedAtUtc
                && token.AbsoluteExpiresAt > revokedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAtUtc)
                .SetProperty(token => token.RevokedByIp, revokedByIp)
                .SetProperty(token => token.ReplacedByTokenHash, replacement.TokenHash)
                .SetProperty(token => token.UpdatedAt, revokedAtUtc)
                .SetProperty(token => token.UpdatedBy, revokedByIp),
                cancellationToken);

        if (affectedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await RefreshTokens.AddAsync(replacement, cancellationToken);
        await base.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
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
    async Task<IAdminTransaction> IAdminDbContext.BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new AdminTransaction(transaction);
    }

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

    // IExerciseDbContext Implementation
    Task<Exercise?> IExerciseDbContext.GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken)
        => Exercises.AsNoTracking()
            .Include(exercise => exercise.TestCases)
            .FirstOrDefaultAsync(exercise => exercise.Slug == slug && exercise.IsPublished, cancellationToken);

    Task<Exercise?> IExerciseDbContext.GetPublishedByIdAsync(Guid exerciseId, CancellationToken cancellationToken)
        => Exercises.AsNoTracking()
            .Include(exercise => exercise.TestCases)
            .FirstOrDefaultAsync(exercise => exercise.Id == exerciseId && exercise.IsPublished, cancellationToken);

    Task IExerciseDbContext.AddSubmissionAsync(Submission submission, CancellationToken cancellationToken)
        => Submissions.AddAsync(submission, cancellationToken).AsTask();

    Task IExerciseDbContext.SaveChangesAsync(CancellationToken cancellationToken)
        => SaveChangesAsync(cancellationToken);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("Email, Student ID, or unique record already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private sealed class AdminTransaction : IAdminTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public AdminTransaction(IDbContextTransaction transaction) => _transaction = transaction;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
