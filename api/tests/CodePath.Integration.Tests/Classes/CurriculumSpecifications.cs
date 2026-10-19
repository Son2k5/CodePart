using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Application.Learning;
using CodePath.Domain.Judge;
using CodePath.Domain.Learning;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Judge;
using CodePath.Infrastructure.Learning;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using CodePath.Shared.Kernel.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace CodePath.Integration.Tests.Classes;

[Collection(IntegrationTestCollection.Name)]
public class CurriculumSpecifications(CodePathApiFixture fixture)
{
    private async Task<(Guid Id, string Token)> Person(UserRole role)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = Guid.NewGuid().ToString("N") + "@hanu.edu.vn"; var now = DateTime.UtcNow;
        var user = role == UserRole.Admin ? User.CreateAdmin("Admin", email, "hash", now) : User.CreateTeacher("Teacher", email, "hash", now);
        user.VerifyEmail(now); if (role == UserRole.Teacher) user.ApproveTeacher(now);
        db.Users.Add(user); await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<JudgeDbContext>().Database.MigrateAsync();
        return (user.Id, await AuthTestClient.IssueSessionAsync(scope.ServiceProvider, user));
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        if (body != null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    private async Task<(Guid Class, Guid Chapter)> Classroom(Guid teacher)
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var c = Class.CreateManaged("Curriculum tests", teacher, DateTime.UtcNow);
        c.Teachers.Add(ClassTeacher.Create(c.Id, teacher, teacher, DateTime.UtcNow));
        var chapter = new Chapter { ClassId = c.Id, Title = "Chapter" };
        db.Classes.Add(c); db.Chapters.Add(chapter); await db.SaveChangesAsync();
        return (c.Id, chapter.Id);
    }

    private static CreateFullTopicInput Full(Guid chapter, string? slug = null) => new(chapter, "Stacks", slug ?? "stacks-" + Guid.NewGuid().ToString("N"), "Summary", 0, 30,
        new LectureContent("# Stack\nUse LIFO"), [], new CompletionRule(), PublishImmediately: true);

    [Fact]
    public async Task Templates_are_visible_to_owner_and_admin_and_shared_admin_templates_are_read_only()
    {
        var (_, admin) = await Person(UserRole.Admin); var (_, owner) = await Person(UserRole.Teacher); var (_, other) = await Person(UserRole.Teacher);
        var template = await Json(await Send(HttpMethod.Post, "/api/curriculum-templates", owner, new CurriculumTemplateInput("Private curriculum", "Private")));
        var id = template.GetProperty("id").GetGuid(); var path = $"/api/curriculum-templates/{id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, path, other)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, $"/api/classes/{id}/chapters", other, new ChapterInput("Denied", 0))).StatusCode);
        await Json(await Send(HttpMethod.Get, path, admin));
        var list = await Json(await Send(HttpMethod.Get, "/api/curriculum-templates", other));
        Assert.DoesNotContain(list.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        var shared = await Json(await Send(HttpMethod.Post, "/api/curriculum-templates", admin, new CurriculumTemplateInput("Shared curriculum", "Shared")));
        var sharedId = shared.GetProperty("id").GetGuid();
        await Json(await Send(HttpMethod.Get, $"/api/curriculum-templates/{sharedId}", other));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Put, $"/api/curriculum-templates/{sharedId}", other, new CurriculumTemplateInput("Changed", ""))).StatusCode);
    }

    [Fact]
    public async Task Composite_publishes_content_and_rejects_other_teachers_and_invalid_inputs_without_partial_topics()
    {
        var (ownerId, owner) = await Person(UserRole.Teacher); var (_, other) = await Person(UserRole.Teacher);
        var (classId, chapter) = await Classroom(ownerId); var path = $"/api/classes/{classId}/topics/complete";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, path, other, Full(chapter))).StatusCode);
        var created = await Json(await Send(HttpMethod.Post, path, owner, Full(chapter)));
        var id = created.GetProperty("id").GetGuid();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(TopicStatus.Published, (await db.Topics.SingleAsync(t => t.Id == id)).Status);
        Assert.True(await db.TopicSearch.AnyAsync(t => t.TopicId == id));
        var asset = new Asset { OwnerId = ownerId, Kind = AssetKind.Pdf, Status = AssetStatus.Pending, MimeType = "application/pdf", StorageKey = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow };
        db.LearningAssets.Add(asset); await db.SaveChangesAsync();
        var invalid = new[] {
            Full(chapter) with { LectureContent = new LectureContent(Video: new("YouTube", "https://evil.example/video", 60)) },
            Full(chapter) with { LectureContent = new LectureContent(Documents: [new(asset.Id, "Pending PDF")]) },
            Full(chapter) with { Exercises = [new(Guid.NewGuid(), 0, -1, true)] },
            Full(chapter) with { Exercises = [new(Guid.NewGuid(), 0, 10001, true)] }
        };
        foreach (var body in invalid)
        {
            Assert.False((await Send(HttpMethod.Post, path, owner, body)).IsSuccessStatusCode);
            Assert.False(await db.Topics.AnyAsync(t => t.ClassId == classId && t.Slug == body.Slug));
        }
    }

    [Fact]
    public async Task Apply_template_preserves_provenance_and_is_idempotent_for_link_and_copy_modes()
    {
        var (teacherId, teacher) = await Person(UserRole.Teacher); var (otherId, other) = await Person(UserRole.Teacher);
        var template = await Json(await Send(HttpMethod.Post, "/api/curriculum-templates", teacher, new CurriculumTemplateInput("Reusable curriculum", "")));
        var templateId = template.GetProperty("id").GetGuid();
        var chapter = await Json(await Send(HttpMethod.Post, $"/api/classes/{templateId}/chapters", teacher, new ChapterInput("Chapter", 0)));
        var chapterId = chapter.GetProperty("id").GetGuid();
        using var scope = fixture.Factory.Services.CreateScope(); var judge = scope.ServiceProvider.GetRequiredService<JudgeDbContext>();
        var problem = new Problem { Title = "Template problem", CreatedBy = teacherId, ClassId = templateId, Status = ProblemStatus.Published };
        judge.Problems.Add(problem); await judge.SaveChangesAsync();
        var full = Full(chapterId) with { Exercises = [new(problem.Id, 0, 10, true)] };
        var topic = await Json(await Send(HttpMethod.Post, $"/api/classes/{templateId}/topics/complete", teacher, full));
        var topicId = topic.GetProperty("id").GetGuid();
        var (otherClass, _) = await Classroom(otherId);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, $"/api/classes/{otherClass}/apply-template", other, new ApplyTemplateInput(templateId), "denied")).StatusCode);
        foreach (var copy in new[] { false, true })
        {
            var (target, _) = await Classroom(teacherId); var path = $"/api/classes/{target}/apply-template"; var key = Guid.NewGuid().ToString("N");
            var input = new ApplyTemplateInput(templateId, [chapterId], copy);
            var result = await Json(await Send(HttpMethod.Post, path, teacher, input, key));
            var retry = await Json(await Send(HttpMethod.Post, path, teacher, input, key)); Assert.Equal(result.ToString(), retry.ToString());
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var copied = await db.Topics.SingleAsync(t => t.ClassId == target);
            Assert.Equal(topicId, copied.SourceTopicId); Assert.Equal(1, copied.SourcePublishedVersion); Assert.Equal(TopicStatus.Draft, copied.Status); Assert.Null(copied.DueAt);
            var exercise = await db.TopicExercises.SingleAsync(e => e.TopicId == copied.Id);
            Assert.Equal(copy, exercise.ProblemId != problem.Id);
            if (copy) Assert.Equal(target, (await judge.Problems.AsNoTracking().SingleAsync(p => p.Id == exercise.ProblemId)).ClassId);
        }
        Assert.False((await Send(HttpMethod.Delete, $"/api/curriculum-templates/{templateId}", teacher)).IsSuccessStatusCode);
        await Json(await Send(HttpMethod.Delete, $"/api/curriculum-templates/{templateId}?confirm=true", teacher));
    }

    [Fact]
    public async Task Linked_problem_read_requires_published_unlocked_topic_and_active_enrollment()
    {
        var (owner, _) = await Person(UserRole.Teacher); var (classId, chapter) = await Classroom(owner);
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>(); var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var student = User.CreateStudent("Student", suffix + "@hanu.edu.vn", "hash", suffix, now);
        db.Users.Add(student);
        var enrollment = ClassEnrollment.Create(classId, student.Id, ClassJoinMethod.Public, now); db.ClassEnrollments.Add(enrollment);
        var classroom = await db.Classes.SingleAsync(c => c.Id == classId); classroom.SetStatus(ClassStatus.Active, now);
        var topic = new Topic { ClassId = classId, ChapterId = chapter, Title = "Linked", Slug = "linked", CreatedBy = owner, UpdatedAt = now };
        var problemId = Guid.NewGuid(); db.Topics.Add(topic); db.TopicExercises.Add(new() { TopicId = topic.Id, ProblemId = problemId });
        await db.SaveChangesAsync();
        var service = services.GetRequiredService<ILearningService>(); var actor = new Actor(student.Id, false, false);
        Assert.False(await service.CanReadLinkedProblem(problemId, actor, default));
        topic.Publish(now); topic.UnlockRule = UnlockRule.AfterDate; topic.UnlockAt = now.AddDays(1); await db.SaveChangesAsync();
        Assert.False(await service.CanReadLinkedProblem(problemId, actor, default));
        topic.UnlockRule = UnlockRule.None; await db.SaveChangesAsync();
        Assert.True(await service.CanReadLinkedProblem(problemId, actor, default));
        enrollment.Remove(); await db.SaveChangesAsync();
        Assert.False(await service.CanReadLinkedProblem(problemId, actor, default));
    }

    [Fact]
    public async Task Available_problems_filters_scopes_and_allows_published_global_exercises()
    {
        var (owner, token) = await Person(UserRole.Teacher); var (other, _) = await Person(UserRole.Teacher);
        var (classId, chapter) = await Classroom(owner); var (otherClass, _) = await Classroom(other);
        using var scope = fixture.Factory.Services.CreateScope(); var judge = scope.ServiceProvider.GetRequiredService<JudgeDbContext>();
        var prefix = Guid.NewGuid().ToString("N");
        var problems = new[] {
            new Problem { Title = prefix + " own", CreatedBy = owner },
            new Problem { Title = prefix + " class", CreatedBy = other, ClassId = classId },
            new Problem { Title = prefix + " global", CreatedBy = other, Status = ProblemStatus.Published },
            new Problem { Title = prefix + " private", CreatedBy = other, ClassId = otherClass },
            new Problem { Title = prefix + " draft", CreatedBy = other },
            new Problem { Title = prefix + " course", CreatedBy = other, CourseId = Guid.NewGuid(), Status = ProblemStatus.Published }
        };
        judge.Problems.AddRange(problems); await judge.SaveChangesAsync();
        var result = await Json(await Send(HttpMethod.Get, $"/api/classes/{classId}/available-problems?search={prefix}&difficulty=Easy&pageSize=2", token));
        Assert.Equal(3, result.GetProperty("total").GetInt32()); Assert.Equal(2, result.GetProperty("items").GetArrayLength());
        await Json(await Send(HttpMethod.Post, $"/api/classes/{classId}/topics/complete", token,
            Full(chapter) with { Exercises = [new(problems[2].Id, 0, 10, true)] }));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, $"/api/classes/{classId}/topics/complete", token,
            Full(chapter) with { Exercises = [new(problems[3].Id, 0, 10, true)] })).StatusCode);
    }

    [Fact]
    public async Task Failure_after_database_save_rolls_back_topic_exercises_search_and_audit()
    {
        var (owner, _) = await Person(UserRole.Teacher); var (classId, chapter) = await Classroom(owner);
        using var scope = fixture.Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        var existing = services.GetRequiredService<AppDbContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySql(existing.Database.GetConnectionString(), new MySqlServerVersion(new Version(8, 4, 0)))
            .AddInterceptors(new FailAfterSave()).Options;
        await using var db = new AppDbContext(options);
        var service = new LearningService(db, services.GetRequiredService<JudgeDbContext>(), TimeProvider.System,
            services.GetRequiredService<IConnectionMultiplexer>(), services.GetRequiredService<IPublisher>(), new ContentValidator(db, services.GetRequiredService<IConfiguration>()));
        var judge = services.GetRequiredService<JudgeDbContext>();
        var problem = new Problem { Title = "Rollback problem", CreatedBy = owner, ClassId = classId };
        judge.Problems.Add(problem); await judge.SaveChangesAsync();
        var request = Full(chapter) with { Exercises = [new(problem.Id, 0, 10, true)] }; var audits = await existing.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFullTopic(classId, request, new Actor(owner, false, true), default));
        Assert.False(await existing.Topics.AnyAsync(t => t.ClassId == classId));
        Assert.False(await existing.TopicSearch.AnyAsync(t => t.ClassId == classId));
        Assert.False(await existing.TopicExercises.AnyAsync(e => e.ProblemId == problem.Id));
        Assert.Equal(audits, await existing.AuditLogs.CountAsync());
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure before transaction commit");
    }
}
