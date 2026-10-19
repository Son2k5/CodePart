using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Infrastructure.Persistence;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class SessionIdleSpecifications(CodePathApiFixture fixture)
{
    private async Task<AuthSession> Login() => await AuthTestClient.RegisterVerifyAndLoginAsync(fixture,
        $"{Random.Shared.Next(100000000, 999999999)}@hanu.edu.vn");

    private async Task<HttpResponseMessage> Refresh(AuthSession session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
        request.Headers.Add("Cookie", $"refreshToken={session.RefreshToken}");
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task IdleSession_ShouldRejectAccessAndRefresh_AndRevokeInDatabase()
    {
        var session = await Login();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<CodePath.Application.Auth.Abstractions.IJwtTokenService>();
        var hash = jwt.HashRefreshToken(Uri.UnescapeDataString(session.RefreshToken));
        var token = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash);
        await db.RefreshTokens.Where(t => t.Id == token.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastActiveAt, DateTime.UtcNow.AddMinutes(-61)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var access = await fixture.Client.SendAsync(request);
        access.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await access.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("SESSION_IDLE_TIMEOUT");
        using var refresh = await Refresh(session);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("SESSION_IDLE_TIMEOUT");
        (await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).RevokedReason.Should().Be("IDLE_TIMEOUT");
        refresh.Headers.GetValues("Set-Cookie").Should().Contain(c => c.Contains("path=/api/auth/refresh-token") && c.StartsWith("refreshToken=;"));
    }

    [Fact]
    public async Task Rotation_ShouldPreservePersistedActivityAndAbsoluteDeadline()
    {
        var session = await Login();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<CodePath.Application.Auth.Abstractions.IJwtTokenService>();
        var hash = jwt.HashRefreshToken(Uri.UnescapeDataString(session.RefreshToken));
        var parent = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash);
        await db.RefreshTokens.Where(t => t.Id == parent.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastActiveAt, DateTime.UtcNow.AddMinutes(-20)));
        parent = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == parent.Id);
        using var refresh = await Refresh(session);
        refresh.EnsureSuccessStatusCode();
        var replacement = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.ParentTokenId == parent.Id);
        replacement.LastActiveAt.Should().Be(parent.LastActiveAt);
        replacement.SessionId.Should().Be(parent.SessionId);
        replacement.AbsoluteExpiresAt.Should().Be(parent.AbsoluteExpiresAt);
        replacement.ExpiresAt.Should().Be(parent.AbsoluteExpiresAt);
    }
}
