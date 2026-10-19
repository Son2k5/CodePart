using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CodePath.Integration.Tests.Infrastructure;

internal static class AuthTestClient
{
    public static async Task<string> IssueSessionAsync(IServiceProvider services, CodePath.Domain.Users.Entities.User user)
    {
        var time = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var jwt = services.GetRequiredService<CodePath.Application.Auth.Abstractions.IJwtTokenService>();
        var db = services.GetRequiredService<CodePath.Infrastructure.Persistence.AppDbContext>();
        var sid = Guid.NewGuid();
        var tokens = jwt.GenerateTokens(user.Id, user.Email, user.Role, user.Status, user.SecurityVersion, sid);
        db.RefreshTokens.Add(CodePath.Domain.Auth.Entities.RefreshToken.Create(user.Id,
            jwt.HashRefreshToken(tokens.RefreshToken), time.AddDays(7), time.AddDays(7), time, sessionId: sid));
        await db.SaveChangesAsync();
        return tokens.AccessToken;
    }

    public const string ValidPassword = "ValidPassword1!";

    public static async Task<AuthSession> RegisterVerifyAndLoginAsync(
        CodePathApiFixture fixture,
        string email,
        CancellationToken cancellationToken = default)
    {
        var registerResponse = await fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Integration Test Student",
            email,
            password = ValidPassword
        }, cancellationToken);
        registerResponse.EnsureSuccessStatusCode();

        var otp = await GetOtpAsync(fixture, email);
        otp.Should().HaveLength(6);

        var verifyResponse = await fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new
        {
            email,
            otp
        }, cancellationToken);
        verifyResponse.EnsureSuccessStatusCode();

        var loginResponse = await fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = ValidPassword
        }, cancellationToken);
        loginResponse.EnsureSuccessStatusCode();

        var payload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = payload.GetProperty("accessToken").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        payload.TryGetProperty("refreshToken", out _).Should().BeFalse();

        var setCookie = loginResponse.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refreshToken=") && !value.StartsWith("refreshToken=;"));
        var refreshToken = setCookie
            .Split(';', 2)[0]
            .Split('=', 2)[1];
        refreshToken.Should().NotBeNullOrWhiteSpace();

        return new AuthSession(accessToken!, refreshToken!, setCookie);
    }

    public static async Task<string> GetOtpAsync(CodePathApiFixture fixture, string email)
    {
        var redis = fixture.Factory.Services.GetRequiredService<IConnectionMultiplexer>();
        var value = await redis.GetDatabase().StringGetAsync($"auth:otp:{email.ToLowerInvariant()}");
        return value.ToString();
    }

    public static IDatabase GetRedisDatabase(CodePathApiFixture fixture)
    {
        var redis = fixture.Factory.Services.GetRequiredService<IConnectionMultiplexer>();
        return redis.GetDatabase();
    }
}

internal sealed record AuthSession(string AccessToken, string RefreshToken, string SetCookie);
