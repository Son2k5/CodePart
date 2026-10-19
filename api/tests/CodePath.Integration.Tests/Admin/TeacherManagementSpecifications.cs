using CodePath.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Shared.Kernel.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Admin;

public sealed partial class AdminWorkflowSpecifications
{
    private async Task<HttpResponseMessage> SendTeacherRequest(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = AuthorizedRequest(method, path, token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task TeacherCrud_FilterStatsExportAndAudit_ShouldUsePersistedData()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (_, _, token) = await SeedAdminAndTeacherAsync($"admin-{suffix}@hanu.edu.vn", $"pending-{suffix}@hanu.edu.vn");
        Guid facultyId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var faculty = Faculty.Create("Test faculty", suffix[..10], DateTime.UtcNow);
            db.Faculties.Add(faculty);
            await db.SaveChangesAsync();
            facultyId = faculty.Id;
        }
        var body = new { fullName = "=Teacher Test", email = $"created-{suffix}@hanu.edu.vn", teacherId = suffix,
            facultyId, phone = "0901234567", bio = "Profile", temporaryPassword = "SecurePassword1!", status = "Active" };
        using var create = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers", token, body);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var managedClass = Class.Create("CS-2026", "2026-2030", facultyId, id, now);
            var course = Course.Create("Algorithms", suffix[..10], "cpp", "2026-1", suffix[..8], id, now);
            var student = User.CreateStudent("Student", $"student-{suffix}@hanu.edu.vn", "hash", suffix, now);
            student.AssignToClass(managedClass.Id, now);
            db.Classes.Add(managedClass);
            db.ClassTeachers.Add(ClassTeacher.Create(managedClass.Id, id, id, now));
            db.ClassEnrollments.Add(ClassEnrollment.Create(managedClass.Id, student.Id, ClassJoinMethod.AdminAdded, now));
            db.Courses.Add(course);
            db.Users.Add(student);
            db.Enrollments.Add(Enrollment.Create(course.Id, student.Id, now));
            await db.SaveChangesAsync();
        }
        using var duplicate = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers", token, body);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var duplicateCode = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers", token,
            new { body.fullName, email = $"other-{suffix}@hanu.edu.vn", body.teacherId, body.temporaryPassword });
        duplicateCode.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var list = await SendTeacherRequest(HttpMethod.Get,
            $"/api/admin/teachers?page=1&pageSize=5&search={suffix}&facultyId={facultyId}&status=Active&sortBy=name_asc", token);
        list.EnsureSuccessStatusCode();
        var page = await list.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
        page.GetProperty("totalPages").GetInt32().Should().Be(1);
        page.GetProperty("items")[0].GetProperty("teacherId").GetString().Should().Be(suffix.ToUpperInvariant());
        page.GetProperty("items")[0].GetProperty("facultyName").GetString().Should().Be("Test faculty");
        page.GetProperty("items")[0].GetProperty("classesCount").GetInt32().Should().Be(1);
        page.GetProperty("items")[0].GetProperty("coursesCount").GetInt32().Should().Be(1);

        using var detail = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/teachers/{id}", token);
        detail.EnsureSuccessStatusCode();
        var profile = await detail.Content.ReadFromJsonAsync<JsonElement>();
        profile.GetProperty("emailVerifiedAt").ValueKind.Should().Be(JsonValueKind.String);
        profile.GetProperty("managedClasses")[0].GetProperty("studentCount").GetInt32().Should().Be(1);
        profile.GetProperty("teachingCourses")[0].GetProperty("enrolledCount").GetInt32().Should().Be(1);

        using var stats = await SendTeacherRequest(HttpMethod.Get, "/api/admin/teachers/stats", token);
        stats.EnsureSuccessStatusCode();
        var kpi = await stats.Content.ReadFromJsonAsync<JsonElement>();
        kpi.GetProperty("totalTeachers").GetInt32().Should().Be(
            new[] { "activeTeachers", "pendingApprovals", "disabledTeachers", "rejectedTeachers" }.Sum(k => kpi.GetProperty(k).GetInt32()));

        using var faculties = await SendTeacherRequest(HttpMethod.Get, "/api/admin/faculties", token);
        faculties.EnsureSuccessStatusCode();
        (await faculties.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Single(f => f.GetProperty("id").GetGuid() == facultyId).GetProperty("teacherCount").GetInt32().Should().Be(1);

        using var export = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/teachers/export?facultyId={facultyId}", token);
        export.EnsureSuccessStatusCode();
        (await export.Content.ReadAsStringAsync()).Should().Contain("'=Teacher Test").And.Contain(body.email);

        using var update = await SendTeacherRequest(HttpMethod.Put, $"/api/admin/teachers/{id}", token,
            new { fullName = "Updated", teacherId = suffix, facultyId = (Guid?)null, phone = (string?)null, bio = "Updated biography" });
        update.EnsureSuccessStatusCode();
        using var updatedDetail = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/teachers/{id}", token);
        var updatedProfile = await updatedDetail.Content.ReadFromJsonAsync<JsonElement>();
        updatedProfile.GetProperty("fullName").GetString().Should().Be("Updated");
        updatedProfile.GetProperty("facultyId").ValueKind.Should().Be(JsonValueKind.Null);
        updatedProfile.GetProperty("phone").ValueKind.Should().Be(JsonValueKind.Null);
        using var audit = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/audit-logs?targetUserId={id}", token);
        audit.EnsureSuccessStatusCode();
        var auditText = await audit.Content.ReadAsStringAsync();
        auditText.Should().Contain("TeacherCreated").And.Contain("TeacherUpdated").And.NotContain(body.temporaryPassword);

        using var invalid = await SendTeacherRequest(HttpMethod.Get, "/api/admin/teachers?page=0", token);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var missing = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/teachers/{Guid.NewGuid()}", token);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var wrongRole = await SendTeacherRequest(HttpMethod.Get, $"/api/admin/teachers/{facultyId}", token);
        wrongRole.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkValidation_ShouldBeAtomic_AndEnableMustInvalidateOldAccessTokens()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (_, teacher, token) = await SeedAdminAndTeacherAsync($"admin-bulk-{suffix}@hanu.edu.vn", $"teacher-bulk-{suffix}@hanu.edu.vn");
        using var invalidBulk = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers/bulk-approve", token,
            new { userIds = new[] { teacher.Id, Guid.NewGuid() } });
        invalidBulk.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.AsNoTracking().SingleAsync(u => u.Id == teacher.Id)).Status.Should().Be(UserStatus.Pending);
            (await db.AuditLogs.CountAsync(a => a.TargetUserId == teacher.Id)).Should().Be(0);
        }
        using var approve = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers/bulk-approve", token,
            new { userIds = new[] { teacher.Id } });
        approve.EnsureSuccessStatusCode();
        string oldToken;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var activeTeacher = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == teacher.Id);
            oldToken = await AuthTestClient.IssueSessionAsync(scope.ServiceProvider, activeTeacher);
        }
        using var forbidden = await SendTeacherRequest(HttpMethod.Get, "/api/admin/teachers/stats", oldToken);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var disable = await SendTeacherRequest(HttpMethod.Post, "/api/admin/teachers/bulk-disable", token,
            new { userIds = new[] { teacher.Id } });
        disable.EnsureSuccessStatusCode();
        using var enable = await SendTeacherRequest(HttpMethod.Post, $"/api/admin/users/{teacher.Id}/enable", token);
        enable.EnsureSuccessStatusCode();
        using var oldSession = await SendTeacherRequest(HttpMethod.Get, "/api/auth/me", oldToken);
        oldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var repeatedEnable = await SendTeacherRequest(HttpMethod.Post, $"/api/admin/users/{teacher.Id}/enable", token);
        repeatedEnable.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
