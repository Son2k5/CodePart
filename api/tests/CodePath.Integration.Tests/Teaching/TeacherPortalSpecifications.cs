using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Domain.Exercises.Enums;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Teaching;

[Collection(IntegrationTestCollection.Name)]
public sealed class TeacherPortalSpecifications(CodePathApiFixture fixture)
{
    private async Task<(User User, string Token)> Person(bool teacher)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("TestPassword1!");
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var user = teacher ? User.CreateTeacher("Teacher", suffix + "@hanu.edu.vn", hash, now)
            : User.CreateStudent("Student", suffix + "@hanu.edu.vn", hash, suffix, now);
        user.VerifyEmail(now);
        if (teacher) user.ApproveTeacher(now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, await AuthTestClient.IssueSessionAsync(scope.ServiceProvider, user));
    }
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token, object? data = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data != null) request.Content = JsonContent.Create(data);
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    [Fact]
    public async Task CourseWorkflow_ShouldEnforceOwnershipAndEnrollment_ProtectHiddenTests_AndPersistTeacherGrade()
    {
        var (teacher, token) = await Person(true);
        var (_, otherTeacher) = await Person(true);
        var (student, studentToken) = await Person(false);
        var (_, outsider) = await Person(false);
        using var factory = fixture.CreateFactory(configureServices: services => services.AddSingleton<ICodeExecutionService>(new EchoRunner()));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        (await Send(client, HttpMethod.Get, "/api/teacher/courses", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Send(client, HttpMethod.Get, "/api/teacher/courses", studentToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var created = await Json(await Send(client, HttpMethod.Post, "/api/teacher/courses", token,
            new { subjectName = "Algorithms", sectionCode = "P2", language = "python", semester = "2026-1", capacity = 1 }));
        var id = created.GetProperty("id").GetGuid();
        var path = $"/api/teacher/courses/{id}";
        var course = await Json(await Send(client, HttpMethod.Get, path, token));
        course.GetProperty("status").GetString().Should().Be("Draft");
        var code = course.GetProperty("joinCode").GetString();
        code.Should().HaveLength(16);
        (await Send(client, HttpMethod.Post, "/api/student/courses/join", studentToken, new { joinCode = code })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Send(client, HttpMethod.Get, path, otherTeacher)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Send(client, HttpMethod.Put, path + "/status", otherTeacher, new { status = "Active" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Json(await Send(client, HttpMethod.Put, path + "/status", token, new { status = "Active" }));
        await Json(await Send(client, HttpMethod.Post, "/api/student/courses/join", studentToken, new { joinCode = code }));
        (await Send(client, HttpMethod.Post, "/api/student/courses/join", studentToken, new { joinCode = code })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Send(client, HttpMethod.Post, "/api/student/courses/join", outsider, new { joinCode = code })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var students = await Json(await Send(client, HttpMethod.Get, path + "/students", token));
        students.GetArrayLength().Should().Be(1);
        students[0].GetProperty("id").GetGuid().Should().Be(student.Id);
        (await Send(client, HttpMethod.Get, path + "/students", otherTeacher)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var exerciseData = new { title = "Echo", difficulty = "Easy", description = "Echo the input", constraints = "None", starterCode = "print(input())",
            timeLimitMs = 1000, memoryLimitKb = 65536,
            testCases = new[] { new { input = "sample", expectedOutput = "sample", isSample = true, explanation = "Example" },
                new { input = "secret-input", expectedOutput = "secret-output", isSample = false, explanation = "Hidden" } } };
        (await Send(client, HttpMethod.Post, path + "/exercises", otherTeacher, exerciseData)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var exercise = await Json(await Send(client, HttpMethod.Post, path + "/exercises", token, exerciseData));
        var exerciseId = exercise.GetProperty("id").GetGuid();
        var assigned = await Json(await Send(client, HttpMethod.Get, $"/api/student/courses/{id}/exercises", studentToken));
        var slug = assigned[0].GetProperty("slug").GetString();
        var publicPath = $"/api/exercises/{slug}";
        foreach (var denied in new string?[] { null, outsider, otherTeacher })
            (await Send(client, HttpMethod.Get, publicPath, denied)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var visible = await Json(await Send(client, HttpMethod.Get, publicPath, studentToken));
        visible.GetProperty("sampleTestCases").GetArrayLength().Should().Be(1);
        visible.GetRawText().Should().NotContain("secret-input").And.NotContain("secret-output");
        var codeBody = new { language = "python", sourceCode = "print(input())" };
        (await Send(client, HttpMethod.Post, $"/api/exercises/{exerciseId}/run", outsider, codeBody)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Send(client, HttpMethod.Post, $"/api/exercises/{exerciseId}/submit", outsider, codeBody)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var submitted = await Json(await Send(client, HttpMethod.Post, $"/api/exercises/{exerciseId}/submit", studentToken, codeBody));
        submitted.GetProperty("status").GetString().Should().Be("Accepted");
        submitted.GetRawText().Should().NotContain("secret-input").And.NotContain("secret-output");
        var submissionId = submitted.GetProperty("submissionId").GetGuid();
        var submissions = await Json(await Send(client, HttpMethod.Get, path + "/submissions?page=1&pageSize=5", token));
        submissions.GetProperty("totalCount").GetInt32().Should().Be(1);
        var detailPath = path + $"/submissions/{submissionId}";
        var detail = await Json(await Send(client, HttpMethod.Get, detailPath, token));
        detail.GetProperty("sourceCode").GetString().Should().Be("print(input())");
        detail.GetProperty("testResults").GetArrayLength().Should().Be(2);
        detail.GetRawText().Should().Contain("secret-output");
        (await Send(client, HttpMethod.Get, detailPath, otherTeacher)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Send(client, HttpMethod.Put, detailPath + "/grade", otherTeacher, new { score = 8, feedback = "No" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Send(client, HttpMethod.Put, detailPath + "/grade", token, new { score = 11, feedback = "Bad score" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Json(await Send(client, HttpMethod.Put, detailPath + "/grade", token, new { score = 8.25, feedback = "Good solution" }));
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Submissions.AsNoTracking().SingleAsync(s => s.Id == submissionId);
            saved.TeacherScore.Should().Be(8.25m);
            saved.GradedBy.Should().Be(teacher.Id);
            saved.GradedAt.Should().NotBeNull();
            saved.Status.Should().Be(SubmissionStatus.Accepted);
        }
        await Json(await Send(client, HttpMethod.Delete, path + $"/students/{student.Id}", token));
        (await Send(client, HttpMethod.Get, publicPath, studentToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Json(await Send(client, HttpMethod.Post, path + "/students", token, new { email = student.Email }));
        await Json(await Send(client, HttpMethod.Put, path + "/status", token, new { status = "Closed" }));
        (await Send(client, HttpMethod.Post, $"/api/exercises/{exerciseId}/submit", studentToken, codeBody)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentJoins_ShouldNotExceedCapacity_AndClassRostersAreOwnerScoped()
    {
        var (teacher, token) = await Person(true);
        var (_, otherTeacher) = await Person(true);
        var (student, first) = await Person(false);
        var (_, second) = await Person(false);
        var client = fixture.Client;
        var created = await Json(await Send(client, HttpMethod.Post, "/api/teacher/courses", token,
            new { subjectName = "Capacity", sectionCode = "CAP", language = "python", semester = "2026", capacity = 1 }));
        var id = created.GetProperty("id").GetGuid();
        var path = $"/api/teacher/courses/{id}";
        await Json(await Send(client, HttpMethod.Put, path + "/status", token, new { status = "Active" }));
        var course = await Json(await Send(client, HttpMethod.Get, path, token));
        var join = new { joinCode = course.GetProperty("joinCode").GetString() };
        var responses = await Task.WhenAll(Send(client, HttpMethod.Post, "/api/student/courses/join", first, join),
            Send(client, HttpMethod.Post, "/api/student/courses/join", second, join));
        responses.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Guid classId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var faculty = Faculty.Create("Faculty", Guid.NewGuid().ToString("N")[..12], now);
            var group = Class.Create("2026-CS", "2026-2030", faculty.Id, teacher.Id, now);
            db.Faculties.Add(faculty);
            db.Classes.Add(group);
            db.ClassTeachers.Add(ClassTeacher.Create(group.Id, teacher.Id, teacher.Id, now));
            db.ClassEnrollments.Add(ClassEnrollment.Create(group.Id, student.Id, ClassJoinMethod.AdminAdded, now));
            (await db.Users.SingleAsync(u => u.Id == student.Id)).AssignToClass(group.Id, now);
            await db.SaveChangesAsync();
            classId = group.Id;
        }
        var classes = await Json(await Send(client, HttpMethod.Get, "/api/teacher/classes", token));
        classes.EnumerateArray().Should().Contain(c => c.GetProperty("id").GetGuid() == classId);
        (await Send(client, HttpMethod.Get, $"/api/teacher/classes/{classId}/students", otherTeacher)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var roster = await Json(await Send(client, HttpMethod.Get, $"/api/teacher/classes/{classId}/students", token));
        roster.GetArrayLength().Should().Be(1);
    }

    private sealed class EchoRunner : ICodeExecutionService
    {
        public bool Supports(string language) => language == "python";
        public Task<CodeExecutionOutcome> ExecuteAsync(string language, string sourceCode, string standardInput,
            string? expectedOutput, int timeLimitMs, int memoryLimitKb, CancellationToken cancellationToken) =>
            Task.FromResult(new CodeExecutionOutcome(SubmissionStatus.Accepted, expectedOutput ?? standardInput, null, 1, 1024));
    }
}
