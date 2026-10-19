using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Common.Exceptions;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Judge;
using CodePath.Infrastructure.Learning;
using CodePath.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using Xunit;
namespace CodePath.Unit.Tests.Learning;

public sealed class RagAccessTests
{
    [Theory]
    [InlineData("other-class")]
    [InlineData("scheduled")]
    [InlineData("draft")]
    [InlineData("previous-incomplete")]
    [InlineData("deleted")]
    [InlineData("inactive-class")]
    public async Task Student_cannot_retrieve_inaccessible_topic(string reason)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var judge = new JudgeDbContext(new DbContextOptionsBuilder<JudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow; var student = Guid.NewGuid();
        var c = Class.CreateManaged("Test class", Guid.NewGuid(), now); c.SetStatus(ClassStatus.Active, now);
        var ch = new Chapter { ClassId = c.Id, Title = "Arrays" };
        var topic = new Topic { ClassId = c.Id, ChapterId = ch.Id, Order = 1 }; topic.Publish(now);
        if (reason == "scheduled") topic.PublishAt = now.AddDays(1);
        if (reason == "draft") topic.Status = TopicStatus.Draft;
        if (reason == "deleted") topic.IsDeleted = true;
        if (reason == "inactive-class") c.SetStatus(ClassStatus.Archived, now);
        if (reason == "previous-incomplete")
        {
            var previous = new Topic { ClassId = c.Id, ChapterId = ch.Id, Order = 0 }; previous.Publish(now);
            db.Topics.Add(previous); topic.UnlockRule = UnlockRule.AfterPreviousCompleted;
        }
        db.Classes.Add(c); db.Chapters.Add(ch); db.Topics.Add(topic);
        db.ClassEnrollments.Add(ClassEnrollment.Create(reason == "other-class" ? Guid.NewGuid() : c.Id, student, ClassJoinMethod.Public, now));
        await db.SaveChangesAsync();
        var service = new LearningService(db, judge, TimeProvider.System, Mock.Of<IConnectionMultiplexer>(), Mock.Of<IPublisher>(), new ContentValidator(db, new ConfigurationBuilder().Build()));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.RagTopic(topic.Id, new(student, false, false), false, default));
    }

    [Fact]
    public async Task Revalidation_observes_teacher_revocation_from_another_context()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        await using var judge = new JudgeDbContext(new DbContextOptionsBuilder<JudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow; var teacher = Guid.NewGuid();
        var c = Class.CreateManaged("Test class", teacher, now); c.SetStatus(ClassStatus.Active, now);
        c.Teachers.Add(ClassTeacher.Create(c.Id, teacher, teacher, now));
        var chapter = new Chapter { ClassId = c.Id }; var topic = new Topic { ClassId = c.Id, ChapterId = chapter.Id }; topic.Publish(now);
        db.Classes.Add(c); db.Chapters.Add(chapter); db.Topics.Add(topic); await db.SaveChangesAsync();
        var service = new LearningService(db, judge, TimeProvider.System, Mock.Of<IConnectionMultiplexer>(), Mock.Of<IPublisher>(), new ContentValidator(db, new ConfigurationBuilder().Build()));
        Assert.Equal(topic.Id, (await service.RagTopic(topic.Id, new(teacher, false, true), true, default)).Id);
        await using (var other = new AppDbContext(options))
        {
            var row = await other.Classes.Include(x => x.Teachers).SingleAsync(x => x.Id == c.Id);
            other.RemoveRange(row.Teachers); await other.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.RagTopic(topic.Id, new(teacher, false, true), true, default));
    }

    [Fact]
    public void Draft_requires_samples_tests_and_valid_quiz_answers()
    {
        var content = new TeachingDraftContent("Sum", "Compute sum", "print(1)",
            Enumerable.Range(0, 10).Select(i => new DraftTest("", "1", i == 0)).ToArray(), [], [Guid.NewGuid().ToString()]);
        RagService.ValidateDraft(content, "exercise");
        Assert.Throws<CodePath.Application.Ai.AiPolicyException>(() => RagService.ValidateDraft(content with { Tests = content.Tests[..2] }, "exercise"));
        Assert.Throws<CodePath.Application.Ai.AiPolicyException>(() => RagService.ValidateDraft(content with { Tests = content.Tests.Select(x => x with { IsSample = false }).ToArray() }, "exercise"));
    }
}
