using CodePath.Application.Users;
using CodePath.Domain.Users.Entities;
using CodePath.Domain.Auditing.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Users;

public sealed class UserRepository(AppDbContext db, TimeProvider clock) : IUserRepository
{
    public Task<User?> GetAsync(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> ClassExistsAsync(Guid id, CancellationToken ct) => db.Classes.AnyAsync(x => x.Id == id, ct);
    public Task<bool> FacultyExistsAsync(Guid id, CancellationToken ct) => db.Faculties.AnyAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<OptionItem>> GetAdministrativeClassOptionsAsync(CancellationToken ct)
    {
        var rows = await db.Classes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        return rows.Select(r => new OptionItem(r.Id, r.Name)).ToList();
    }
    public async Task<IReadOnlyList<OptionItem>> GetFacultyOptionsAsync(CancellationToken ct)
    {
        var rows = await db.Faculties.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        return rows.Select(r => new OptionItem(r.Id, r.Name)).ToList();
    }
    public async Task SaveAsync(User user, int expectedVersion, AuditLog? audit, bool revokeSessions, CancellationToken ct)
    {
        if (user.Version != expectedVersion) throw new ConflictException("Profile changed. Reload and try again.");
        db.Entry(user).Property(x => x.Version).OriginalValue = expectedVersion;
        if (audit is not null) db.AuditLogs.Add(audit);
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                // Keep entity states until commit so a transient failure can retry the whole transaction.
                await db.SaveChangesAsync(false, ct);
                if (revokeSessions)
                {
                    var now = clock.GetUtcNow().UtcDateTime;
                    await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now)
                            .SetProperty(x => x.RevokedReason, "SECURITY_CHANGED"), ct);
                }
                await transaction.CommitAsync(ct);
            });
            db.ChangeTracker.AcceptAllChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Profile changed. Reload and try again.");
        }
    }
}
