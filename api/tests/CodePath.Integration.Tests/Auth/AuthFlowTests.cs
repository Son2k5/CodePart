using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AuthFlowTests
{
    private readonly CodePathApiFixture _fixture;

    public AuthFlowTests(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RegisterVerifyLoginAndGetProfile_ShouldSucceed()
    {
        const string email = "2026000001@hanu.edu.vn";
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, email);

        session.SetCookie.Should().Contain("refreshToken=");
        session.SetCookie.ToLowerInvariant().Should().Contain("httponly");
        session.SetCookie.ToLowerInvariant().Should().Contain("samesite=strict");
        session.SetCookie.ToLowerInvariant().Should().Contain("secure");
        session.SetCookie.ToLowerInvariant().Should().Contain("path=/api/auth");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var response = await _fixture.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        profile.GetProperty("email").GetString().Should().Be(email);
        profile.GetProperty("role").GetString().Should().Be("Student");
        profile.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        const string email = "2026000002@hanu.edu.vn";
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, email);

        using var response = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyOtp_AfterFiveInvalidAttempts_ShouldRejectCorrectOtp()
    {
        const string email = "2026000003@hanu.edu.vn";
        using var registerResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "OTP Attempt Test",
            email,
            password = AuthTestClient.ValidPassword
        });
        registerResponse.EnsureSuccessStatusCode();

        var correctOtp = await AuthTestClient.GetOtpAsync(_fixture, email);
        var wrongOtp = correctOtp == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var invalidResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new
            {
                email,
                otp = wrongOtp
            });
            invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var exhaustedResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new
        {
            email,
            otp = correctOtp
        });
        exhaustedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var redis = AuthTestClient.GetRedisDatabase(_fixture);
        (await redis.KeyExistsAsync($"auth:otp:{email}")).Should().BeFalse();
        (await redis.KeyExistsAsync($"auth:otp:attempts:{email}")).Should().BeFalse();
    }

    [Fact]
    public async Task HealthEndpoints_ShouldReportApiAndRedisHealthy()
    {
        using var apiResponse = await _fixture.Client.GetAsync("/health");
        using var redisResponse = await _fixture.Client.GetAsync("/health/redis");

        apiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        redisResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
