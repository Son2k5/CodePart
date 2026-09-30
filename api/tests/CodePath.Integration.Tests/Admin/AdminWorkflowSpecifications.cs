using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Auth.Abstractions;
using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Admin;

[Collection(IntegrationTestCollection.Name)]
public sealed class AdminWorkflowSpecifications
{
    private readonly CodePathApiFixture _fixture;

    public AdminWorkflowSpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AdminWorkflow_ShouldApproveDisableAuditAndInvalidateExistingSessions()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var teacherEmail = $"teacher-{suffix}@hanu.edu.vn";
        var (admin, teacher, adminAccessToken) = await SeedAdminAndTeacherAsync(
            $"admin-{suffix}@hanu.edu.vn",
            teacherEmail);

        using var pendingTeachersRequest = AuthorizedRequest(
            HttpMethod.Get,
            "/api/admin/teachers?status=Pending&limit=10",
            adminAccessToken);
        using var pendingTeachersResponse = await _fixture.Client.SendAsync(pendingTeachersRequest);
        pendingTeachersResponse.EnsureSuccessStatusCode();
        var pendingTeachers = await pendingTeachersResponse.Content.ReadFromJsonAsync<JsonElement>();
        pendingTeachers.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetGuid() == teacher.Id);

        using var approveRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/admin/users/{teacher.Id}/approve",
            adminAccessToken);
        using var approveResponse = await _fixture.Client.SendAsync(approveRequest);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var loginResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = teacherEmail,
            password = AuthTestClient.ValidPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var teacherAccessToken = loginPayload.GetProperty("accessToken").GetString()!;
        var refreshToken = loginResponse.Headers.GetValues("Set-Cookie").Single()
            .Split(';', 2)[0]
            .Split('=', 2)[1];

        // Prime the status cache so this test proves that disable invalidates it.
        using var activeMeRequest = AuthorizedRequest(HttpMethod.Get, "/api/auth/me", teacherAccessToken);
        using var activeMeResponse = await _fixture.Client.SendAsync(activeMeRequest);
        activeMeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var disableRequest = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/admin/users/{teacher.Id}/disable",
            adminAccessToken);
        using var disableResponse = await _fixture.Client.SendAsync(disableRequest);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var disablePayload = await disableResponse.Content.ReadFromJsonAsync<JsonElement>();
        disablePayload.GetProperty("revokedSessionCount").GetInt32().Should().BeGreaterThan(0);

        using var staleAccessRequest = AuthorizedRequest(HttpMethod.Get, "/api/auth/me", teacherAccessToken);
        using var staleAccessResponse = await _fixture.Client.SendAsync(staleAccessRequest);
        staleAccessResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        using var refreshResponse = await _fixture.Client.SendAsync(refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var auditRequest = AuthorizedRequest(
            HttpMethod.Get,
            $"/api/admin/audit-logs?targetUserId={teacher.Id}&limit=10",
            adminAccessToken);
        using var auditResponse = await _fixture.Client.SendAsync(auditRequest);
        auditResponse.EnsureSuccessStatusCode();
        var auditPayload = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var auditItems = auditPayload.GetProperty("items").EnumerateArray().ToArray();

        auditItems.Should().HaveCount(2);
        auditItems.Select(item => item.GetProperty("action").GetString())
            .Should().BeEquivalentTo("TeacherApproved", "UserDisabled");
        auditItems.Should().OnlyContain(item => item.GetProperty("actorId").GetGuid() == admin.Id);

        var serializedAudit = auditPayload.GetRawText();
        var normalizedAudit = serializedAudit.ToLowerInvariant();
        normalizedAudit.Should().NotContain("password");
        normalizedAudit.Should().NotContain("token");
        serializedAudit.Should().NotContain(refreshToken);
    }

    [Fact]
    public async Task DisableSelf_ShouldReturnForbiddenWithoutAuditRecord()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (admin, _, adminAccessToken) = await SeedAdminAndTeacherAsync(
            $"admin-self-{suffix}@hanu.edu.vn",
            $"teacher-self-{suffix}@hanu.edu.vn");

        using var request = AuthorizedRequest(
            HttpMethod.Post,
            $"/api/admin/users/{admin.Id}/disable",
            adminAccessToken);
        using var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(User Admin, User Teacher, string AdminAccessToken)> SeedAdminAndTeacherAsync(
        string adminEmail,
        string teacherEmail)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var passwordHash = passwordHasher.HashPassword(AuthTestClient.ValidPassword);

        var now = DateTime.UtcNow;
        var admin = User.CreateAdmin("Workflow Admin", adminEmail, passwordHash, now);
        var teacher = User.CreateTeacher("Workflow Teacher", teacherEmail, passwordHash, now);
        teacher.VerifyEmail(now);

        await dbContext.Users.AddRangeAsync(admin, teacher);
        await dbContext.SaveChangesAsync();

        var adminAccessToken = jwtTokenService.GenerateTokens(
            admin.Id,
            admin.Email,
            admin.Role,
            admin.Status).AccessToken;

        return (admin, teacher, adminAccessToken);
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
