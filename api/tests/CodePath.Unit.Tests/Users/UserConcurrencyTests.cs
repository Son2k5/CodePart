using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CodePath.Unit.Tests.Users;

public sealed class UserConcurrencyTests
{
    [Fact]
    public async Task Concurrent_profile_writers_cannot_overwrite_each_other()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        await using var first = new AppDbContext(options);
        var user = User.CreateStudent("Student Name", "test@example.com", "hash", "SV001", DateTime.UtcNow);
        first.Users.Add(user);
        await first.SaveChangesAsync();
        await using var second = new AppDbContext(options);
        var stale = await second.Users.SingleAsync();
        user.UpdateFullName("First Writer", DateTime.UtcNow);
        await first.SaveChangesAsync();
        Assert.Equal(2, user.Version);
        stale.UpdateFullName("Second Writer", DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Password_change_conflicts_with_a_concurrent_security_revocation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        await using var first = new AppDbContext(options);
        var user = User.CreateStudent("Student Name", "test@example.com", "hash", "SV001", DateTime.UtcNow);
        first.Users.Add(user);
        await first.SaveChangesAsync();
        await using var second = new AppDbContext(options);
        var stale = await second.Users.SingleAsync();
        user.IncrementSecurityVersion(DateTime.UtcNow);
        await first.SaveChangesAsync();
        stale.ChangePasswordHash("new-hash", DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(false));
    }
}
