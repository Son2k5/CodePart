using Xunit;
using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using CodePath.Infrastructure.Judge;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodePath.Unit.Tests.Judge;

public class PracticeLibraryTests
{
    [Fact]
    public async Task Library_filters_before_pagination_and_never_exposes_class_or_draft_problems()
    {
        await using var db = new JudgeDbContext(new DbContextOptionsBuilder<JudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var access = new Mock<IJudgeAccess>(MockBehavior.Strict);
        var service = new JudgeService(db, access.Object, Mock.Of<ICodeExecutor>(), new JudgeQueue());
        var actor = new Actor(Guid.NewGuid(), false, false);
        for (var i = 0; i < 23; i++) db.Problems.Add(Create($"Array {i}"));
        var privateProblem = Create("Array private"); privateProblem.ClassId = Guid.NewGuid(); db.Problems.Add(privateProblem);
        var draft = Create("Array draft"); draft.Status = ProblemStatus.Draft; db.Problems.Add(draft);
        var deleted = Create("Array deleted"); deleted.IsDeleted = true; db.Problems.Add(deleted);
        var other = Create("Other"); other.Category = "strings"; db.Problems.Add(other);
        var hard = Create("Array hard"); hard.Difficulty = "Hard"; db.Problems.Add(hard);
        var java = Create("Array Java"); java.StarterCodes = [new() { ProblemId = java.Id, Language = "java", Template = "" }]; db.Problems.Add(java);
        await db.SaveChangesAsync();
        var first = await service.ListProblems(actor, false, 1, default, library: true, search: "Array", category: "arrays", difficulty: "Easy", language: "python");
        var second = await service.ListProblems(actor, false, 2, default, library: true, search: "Array", category: "arrays", difficulty: "Easy", language: "python");
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        var a = JsonSerializer.SerializeToElement(first.Value);
        var b = JsonSerializer.SerializeToElement(second.Value);
        Assert.Equal(23, a.GetProperty("Total").GetInt32());
        Assert.Equal(20, a.GetProperty("Items").GetArrayLength());
        Assert.Equal(3, b.GetProperty("Items").GetArrayLength());
        var ids = a.GetProperty("Items").EnumerateArray().Concat(b.GetProperty("Items").EnumerateArray()).Select(p => p.GetProperty("Id").GetGuid()).ToArray();
        Assert.Equal(23, ids.Distinct().Count());
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Library_rejects_combining_private_class_scope()
    {
        await using var db = new JudgeDbContext(new DbContextOptionsBuilder<JudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new JudgeService(db, Mock.Of<IJudgeAccess>(), Mock.Of<ICodeExecutor>(), new JudgeQueue());
        var result = await service.ListProblems(new(Guid.NewGuid(), false, false), false, 1, default, classId: Guid.NewGuid(), library: true);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Progress_counts_distinct_accepted_problems_for_current_user_before_pagination()
    {
        await using var db = new JudgeDbContext(new DbContextOptionsBuilder<JudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = new Actor(Guid.NewGuid(), false, false);
        var problems = Enumerable.Range(0, 23).Select(i => Create($"Array {i}")).ToArray();
        db.Problems.AddRange(problems);
        var hidden = Create("Private"); hidden.ClassId = Guid.NewGuid(); db.Problems.Add(hidden);
        db.Submissions.AddRange(
            new Submission { UserId = actor.Id, ProblemId = problems[0].Id, Status = Verdict.Accepted },
            new Submission { UserId = actor.Id, ProblemId = problems[0].Id, Status = Verdict.Accepted },
            new Submission { UserId = actor.Id, ProblemId = problems[22].Id, Status = Verdict.Accepted },
            new Submission { UserId = actor.Id, ProblemId = problems[1].Id, Status = Verdict.WrongAnswer },
            new Submission { UserId = Guid.NewGuid(), ProblemId = problems[2].Id, Status = Verdict.Accepted },
            new Submission { UserId = actor.Id, ProblemId = hidden.Id, Status = Verdict.Accepted });
        await db.SaveChangesAsync();
        var service = new JudgeService(db, Mock.Of<IJudgeAccess>(), Mock.Of<ICodeExecutor>(), new JudgeQueue());
        foreach (var page in new[] { 1, 2 })
        {
            var result = await service.ListProblems(actor, false, page, default, library: true);
            Assert.True(result.IsSuccess);
            var data = JsonSerializer.SerializeToElement(result.Value);
            var progress = Assert.Single(data.GetProperty("Progress").EnumerateArray());
            Assert.Equal("Easy", progress.GetProperty("Difficulty").GetString());
            Assert.Equal(23, progress.GetProperty("Total").GetInt32());
            Assert.Equal(2, progress.GetProperty("Solved").GetInt32());
        }
        var empty = await service.ListProblems(actor, false, 1, default, library: true, difficulty: "Hard");
        Assert.Empty(JsonSerializer.SerializeToElement(empty.Value).GetProperty("Progress").EnumerateArray());
    }

    private static Problem Create(string title)
    {
        var p = new Problem { Title = title, Category = "arrays", Status = ProblemStatus.Published, CreatedAt = new DateTime(2026, 10, 24) };
        p.StarterCodes.Add(new() { ProblemId = p.Id, Language = "python", Template = "" });
        return p;
    }
}
