using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Classes;

[Collection(IntegrationTestCollection.Name)]
public class ClassManagementSpecifications(CodePathApiFixture fixture)
{
    private async Task<(Guid Id, string Token)> Person(UserRole role)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow;
        var user = role switch {
            UserRole.Admin => User.CreateAdmin("Admin", suffix + "@hanu.edu.vn", "hash", now),
            UserRole.Teacher => User.CreateTeacher("Teacher", suffix + "@hanu.edu.vn", "hash", now),
            _ => User.CreateStudent("Student", suffix + "@hanu.edu.vn", "hash", suffix, now)
        };
        user.VerifyEmail(now); if (role == UserRole.Teacher) user.ApproveTeacher(now);
        db.Users.Add(user); await db.SaveChangesAsync();
        return (user.Id, await AuthTestClient.IssueSessionAsync(scope.ServiceProvider, user));
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        using var r = new HttpRequestMessage(method, path);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) r.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(r);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync(); Assert.True(r.IsSuccessStatusCode, $"{r.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }
    private static async Task Code(HttpResponseMessage r, string expected) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(expected);
    private static object Body(string name, string access = "Public", Guid? version = null, int? max = 1) =>
        new { name, description = "Integration class", status = "Active", accessType = access, maxStudents = max, rowVersion = version };

    [Fact]
    public async Task Workflow_enforces_permissions_concurrency_codes_assignments_and_soft_delete()
    {
        var (_, admin) = await Person(UserRole.Admin); var (teacherId, teacher) = await Person(UserRole.Teacher);
        var (studentId, student) = await Person(UserRole.Student);
        var name = "Class " + Guid.NewGuid().ToString("N");
        var created = await Json(await Send(HttpMethod.Post, "/api/admin/classes", admin, Body(name, "CodeRequired")));
        var id = created.GetProperty("id").GetGuid(); var path = $"/api/admin/classes/{id}";
        var detail = await Json(await Send(HttpMethod.Get, path, admin));
        var code = detail.GetProperty("joinCode").GetString(); var version = detail.GetProperty("rowVersion").GetGuid();
        detail.GetProperty("teachers").GetArrayLength().Should().Be(0);
        (await Send(HttpMethod.Get, path, teacher)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Send(HttpMethod.Get, path, student)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await Code(await Send(HttpMethod.Post, path + "/teachers", admin, new { teacherIds = new[] { studentId } }), "TEACHER_ROLE_REQUIRED");
        await Json(await Send(HttpMethod.Post, path + "/teachers", admin, new { teacherIds = new[] { teacherId, teacherId } }));
        var filtered = await Json(await Send(HttpMethod.Get,
            $"/api/admin/classes?search={code!.ToLowerInvariant()}&hasTeacher=true&teacherId={teacherId}&status=Active&accessType=CodeRequired&sort=studentCount&desc=false&pageSize=1", admin));
        filtered.GetProperty("totalCount").GetInt32().Should().Be(1);
        filtered.GetProperty("items")[0].GetProperty("teachers").GetArrayLength().Should().Be(1);
        var assigned = await Json(await Send(HttpMethod.Get, "/api/teacher/classes", teacher));
        assigned.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);
        assigned.GetRawText().Should().NotContain("joinCode");
        await Code(await Send(HttpMethod.Put, path, admin, Body(name, version: version)), "CLASS_CONCURRENCY_CONFLICT");
        await Json(await Send(HttpMethod.Post, "/api/classes/join-by-code", student, new { code }));
        await Code(await Send(HttpMethod.Post, "/api/classes/join-by-code", student, new { code }), "ALREADY_ENROLLED");
        await Code(await Send(HttpMethod.Delete, path, admin), "CLASS_HAS_STUDENTS");
        await Code(await Send(HttpMethod.Delete, path + "?force=true", admin), "CLASS_HAS_STUDENTS");
        var members = await Json(await Send(HttpMethod.Get, path + "/students?page=1&pageSize=10", admin));
        members.GetProperty("totalCount").GetInt32().Should().Be(1);
        detail = await Json(await Send(HttpMethod.Get, path, admin));
        await Json(await Send(HttpMethod.Put, path, admin, Body(name, version: detail.GetProperty("rowVersion").GetGuid())));
        detail = await Json(await Send(HttpMethod.Get, path, admin)); detail.GetProperty("joinCode").ValueKind.Should().Be(JsonValueKind.Null);
        var list = await Json(await Send(HttpMethod.Get, $"/api/classes/public?search={Uri.EscapeDataString(name)}", student));
        list.GetProperty("totalCount").GetInt32().Should().Be(1); list.GetRawText().Should().NotContain("joinCode");
        await Json(await Send(HttpMethod.Put, path, admin, Body(name, "CodeRequired", detail.GetProperty("rowVersion").GetGuid())));
        detail = await Json(await Send(HttpMethod.Get, path, admin)); detail.GetProperty("joinCode").GetString().Should().NotBe(code);
        await Json(await Send(HttpMethod.Post, path + "/join-code/regenerate", admin, new { expiresAt = DateTime.UtcNow.AddHours(1) }));
        detail = await Json(await Send(HttpMethod.Get, path, admin)); var regenerated = detail.GetProperty("joinCode").GetString();
        await Json(await Send(HttpMethod.Delete, path + "/join-code", admin));
        await Code(await Send(HttpMethod.Post, "/api/classes/join-by-code", student, new { code = regenerated }), "INVALID_JOIN_CODE");
        await Json(await Send(HttpMethod.Delete, path + $"/teachers/{teacherId}", admin));
        (await Send(HttpMethod.Get, $"/api/teacher/classes/{id}/students", teacher)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Json(await Send(HttpMethod.Delete, path + "?force=true&confirm=true", admin));
        (await Send(HttpMethod.Get, path, admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Classes.IgnoreQueryFilters().SingleAsync(c => c.Id == id)).IsDeleted.Should().BeTrue();
        (await db.ClassEnrollments.IgnoreQueryFilters().SingleAsync(e => e.ClassId == id)).Status.Should().Be(ClassEnrollmentStatus.Removed);
        var audit = await db.AuditLogs.Select(a => a.DataJson).ToListAsync();
        audit.Count(data => data.Contains(id.ToString())).Should().BeGreaterThanOrEqualTo(8);
    }
    [Fact]
    public async Task Parallel_joins_cannot_overbook_last_seat_and_removed_student_can_rejoin()
    {
        var (_, admin) = await Person(UserRole.Admin);
        var first = await Person(UserRole.Student); var second = await Person(UserRole.Student);
        var created = await Json(await Send(HttpMethod.Post, "/api/admin/classes", admin, Body("Race " + Guid.NewGuid())));
        var id = created.GetProperty("id").GetGuid(); var path = $"/api/classes/{id}";
        var results = await Task.WhenAll(Send(HttpMethod.Post, path + "/join", first.Token), Send(HttpMethod.Post, path + "/join", second.Token));
        results.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        await Code(results.Single(r => !r.IsSuccessStatusCode), "CLASS_FULL");
        var winner = results[0].IsSuccessStatusCode ? first : second;
        await Json(await Send(HttpMethod.Delete, $"/api/admin/classes/{id}/students/{winner.Id}", admin));
        await Json(await Send(HttpMethod.Post, path + "/join", winner.Token));
        await Json(await Send(HttpMethod.Post, path + "/leave", winner.Token));
        await Json(await Send(HttpMethod.Delete, $"/api/admin/classes/{id}", admin));
    }
    [Fact]
    public async Task Join_code_rate_limit_is_per_user()
    {
        var (_, student) = await Person(UserRole.Student); var (_, other) = await Person(UserRole.Student);
        for (var i = 0; i < 5; i++) (await Send(HttpMethod.Post, "/api/classes/join-by-code", student, new { code = "ZZZZZZZZ" })).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        (await Send(HttpMethod.Post, "/api/classes/join-by-code", student, new { code = "ZZZZZZZZ" })).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Send(HttpMethod.Post, "/api/classes/join-by-code", other, new { code = "ZZZZZZZZ" })).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
