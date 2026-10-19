using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Learning;
using CodePath.Infrastructure.Learning;
using CodePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
namespace CodePath.Unit.Tests.Learning;

public sealed class RagTests
{
    private sealed class BeforeSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw new StopBeforeIo();
    }
    private sealed class StopBeforeIo : Exception;
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=127.0.0.1;Database=unused;User=unused", new MySqlServerVersion(new Version(8, 4, 0)))
        .AddInterceptors(new BeforeSave()).Options);

    [Fact]
    public async Task Published_topic_and_outbox_are_in_same_save_and_retry_does_not_duplicate_job()
    {
        using var db = Database(); var topic = new Topic(); topic.Publish(DateTime.UtcNow); db.Topics.Add(topic);
        await Assert.ThrowsAsync<StopBeforeIo>(() => db.SaveChangesAsync());
        var job = Assert.Single(db.RagIndexJobs.Local);
        Assert.Equal(topic.Id, job.TopicId); Assert.Equal(topic.RowVersion, job.Revision);
        await Assert.ThrowsAsync<StopBeforeIo>(() => db.SaveChangesAsync());
        Assert.Single(db.RagIndexJobs.Local);
    }
    [Fact]
    public async Task Drafts_are_not_indexed_and_unpublishing_enqueues_revocation()
    {
        using var db = Database(); var topic = new Topic(); db.Topics.Add(topic);
        await Assert.ThrowsAsync<StopBeforeIo>(() => db.SaveChangesAsync()); Assert.Empty(db.RagIndexJobs.Local);
        topic.Publish(DateTime.UtcNow); topic.Status = TopicStatus.Draft;
        await Assert.ThrowsAsync<StopBeforeIo>(() => db.SaveChangesAsync()); Assert.Single(db.RagIndexJobs.Local);
    }
    [Theory]
    [InlineData("revision")]
    [InlineData("class")]
    [InlineData("version")]
    [InlineData("block")]
    [InlineData("revoked")]
    public async Task Revalidation_withholds_stale_or_unauthorized_sources(string change)
    {
        using var db = Database(); var access = new Mock<ILearningRagAccess>();
        var id = Guid.NewGuid(); var block = Guid.NewGuid(); var revision = Guid.NewGuid(); var classId = Guid.NewGuid();
        var topic = new RagTopic(id, classId, revision, 1, "Arrays", "arrays", JsonSerializer.Serialize(new[] {
            new ContentBlock(block, "Markdown", JsonSerializer.SerializeToElement(new { markdown = "Array indexes." })) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var p = new RagPassage(Guid.NewGuid(), block, block, id, classId, revision, 1, "Arrays", null, "", "Array indexes.", .9);
        if (change == "revision") topic = topic with { Revision = Guid.NewGuid() };
        if (change == "class") topic = topic with { ClassId = Guid.NewGuid() };
        if (change == "version") topic = topic with { Version = 2 };
        if (change == "block") topic = topic with { Content = "[]" };
        if (change == "revoked") access.Setup(x => x.RagTopic(id, It.IsAny<Actor>(), false, default)).ThrowsAsync(new DomainRuleViolationException("Revoked", "FORBIDDEN"));
        else access.Setup(x => x.RagTopic(id, It.IsAny<Actor>(), false, default)).ReturnsAsync(topic);
        var service = new RagService(db, access.Object, Mock.Of<IRagClient>(), Mock.Of<IFileStorage>(),
            Mock.Of<StackExchange.Redis.IConnectionMultiplexer>(), new ConfigurationBuilder().Build(), NullLogger<RagService>.Instance);
        Assert.False(await service.StillAllowed([p], new(Guid.NewGuid(), false, false), default));
    }
}
