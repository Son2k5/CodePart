using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using StackExchange.Redis;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "SecurityRegression")]
public sealed class AuthSecurityRegressionSpecifications
{
    private readonly CodePathApiFixture _fixture;

    public AuthSecurityRegressionSpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ConcurrentRefresh_WithSameToken_ShouldIssueOnlyOneReplacement()
    {
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(
            _fixture,
            "2026000011@hanu.edu.vn");

        var refreshTasks = Enumerable.Range(0, 20)
            .Select(_ => SendCookieOnlyRequestAsync("/api/auth/refresh", session.RefreshToken))
            .ToArray();

        var responses = await Task.WhenAll(refreshTasks);
        var statuses = responses.Select(response => response.StatusCode).ToArray();

        statuses.Count(status => status == HttpStatusCode.OK).Should().Be(1);
        statuses.Count(status => status == HttpStatusCode.Unauthorized).Should().Be(19);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task ReusingRotatedRefreshToken_ShouldReturnUnauthorized()
    {
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(
            _fixture,
            "2026000012@hanu.edu.vn");

        using var rotation = await SendCookieOnlyRequestAsync("/api/auth/refresh", session.RefreshToken);
        rotation.EnsureSuccessStatusCode();
        var replacementCookie = rotation.Headers.GetValues("Set-Cookie").Single();
        var replacementToken = replacementCookie.Split(';', 2)[0].Split('=', 2)[1];

        using var reuse = await SendCookieOnlyRequestAsync("/api/auth/refresh", session.RefreshToken);

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var familyRevoked = await SendCookieOnlyRequestAsync("/api/auth/refresh", replacementToken);
        familyRevoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginResponse_ShouldNotExposeRefreshTokenInJson()
    {
        const string email = "2026000013@hanu.edu.vn";
        var registerResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Cookie Only Test",
            email,
            password = AuthTestClient.ValidPassword
        });
        registerResponse.EnsureSuccessStatusCode();

        var otp = await AuthTestClient.GetOtpAsync(_fixture, email);
        var verifyResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new { email, otp });
        verifyResponse.EnsureSuccessStatusCode();

        using var loginResponse = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = AuthTestClient.ValidPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var json = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("refreshToken", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Logout_WithoutAccessToken_ShouldRevokeSession()
    {
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(
            _fixture,
            "2026000014@hanu.edu.vn");

        using var response = await SendCookieOnlyRequestAsync("/api/auth/logout", session.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var repeatedLogout = await SendCookieOnlyRequestAsync("/api/auth/logout", session.RefreshToken);
        repeatedLogout.StatusCode.Should().Be(HttpStatusCode.OK);

        using var refresh = await SendCookieOnlyRequestAsync("/api/auth/refresh", session.RefreshToken);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithTokenInBodyOnly_ShouldReturnUnauthorized()
    {
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(
            _fixture,
            "2026000018@hanu.edu.vn");

        using var response = await _fixture.Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutAll_ShouldRevokeEveryRefreshTokenForCurrentUser()
    {
        var session = await AuthTestClient.RegisterVerifyAndLoginAsync(
            _fixture,
            "2026000019@hanu.edu.vn");

        using var logoutAllRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout-all");
        logoutAllRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var logoutAll = await _fixture.Client.SendAsync(logoutAllRequest);

        logoutAll.StatusCode.Should().Be(HttpStatusCode.OK);

        using var refresh = await SendCookieOnlyRequestAsync("/api/auth/refresh", session.RefreshToken);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RepeatedRegister_ShouldNotResetActiveOtpOrAttemptCounter()
    {
        const string email = "2026000015@hanu.edu.vn";
        var registration = new
        {
            fullName = "OTP Reset Test",
            email,
            password = AuthTestClient.ValidPassword
        };

        using var firstRegister = await _fixture.Client.PostAsJsonAsync("/api/auth/register", registration);
        firstRegister.EnsureSuccessStatusCode();
        var originalOtp = await AuthTestClient.GetOtpAsync(_fixture, email);

        var wrongOtp = originalOtp == "000000" ? "111111" : "000000";
        using var wrongAttempt = await _fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new
        {
            email,
            otp = wrongOtp
        });
        wrongAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var repeatedRegister = await _fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Changed Account Data",
            email,
            password = "DifferentPassword2!"
        });
        repeatedRegister.EnsureSuccessStatusCode();

        var redis = AuthTestClient.GetRedisDatabase(_fixture);
        var currentOtp = await redis.StringGetAsync($"auth:otp:{email}");
        var remainingAttempts = await redis.StringGetAsync($"auth:otp:attempts:{email}");

        currentOtp.ToString().Should().Be(originalOtp);
        remainingAttempts.ToString().Should().Be("4");

        using var verify = await _fixture.Client.PostAsJsonAsync("/api/auth/verify-otp", new { email, otp = originalOtp });
        verify.EnsureSuccessStatusCode();

        using var originalPasswordLogin = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = AuthTestClient.ValidPassword
        });
        originalPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        using var changedPasswordLogin = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "DifferentPassword2!"
        });
        changedPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtpRequests_ShouldEnforceHourlyQuotaPerEmail()
    {
        const string email = "2026000020@hanu.edu.vn";
        using var register = await _fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "OTP Quota Test",
            email,
            password = AuthTestClient.ValidPassword
        });
        register.EnsureSuccessStatusCode();

        var redis = AuthTestClient.GetRedisDatabase(_fixture);
        for (var issuedCount = 2; issuedCount <= 5; issuedCount++)
        {
            await DeleteCurrentOtpAsync(redis, email);
            using var resend = await _fixture.Client.PostAsJsonAsync("/api/auth/resend-otp", new { email });
            resend.EnsureSuccessStatusCode();
            (await redis.KeyExistsAsync($"auth:otp:{email}")).Should().BeTrue();
        }

        await DeleteCurrentOtpAsync(redis, email);
        using var quotaExceeded = await _fixture.Client.PostAsJsonAsync("/api/auth/resend-otp", new { email });

        quotaExceeded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await redis.KeyExistsAsync($"auth:otp:{email}")).Should().BeFalse();
    }

    [Fact]
    public async Task Register_WithPasswordOverMaximumLength_ShouldReturnBadRequest()
    {
        using var response = await _fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Long Password Test",
            email = "2026000021@hanu.edu.vn",
            password = new string('a', 129)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SuccessfulLogin_ShouldResetAccountFailureCounter()
    {
        const string email = "2026000022@hanu.edu.vn";
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, email);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failed = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "WrongPassword1!"
            });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var successful = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = AuthTestClient.ValidPassword
        });
        successful.StatusCode.Should().Be(HttpStatusCode.OK);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failed = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "WrongPassword1!"
            });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task ResendOtp_ShouldUseNeutralResponseForUnknownAndVerifiedAccounts()
    {
        const string verifiedEmail = "2026000023@hanu.edu.vn";
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, verifiedEmail);

        using var verifiedResponse = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/resend-otp",
            new { email = verifiedEmail });
        using var unknownResponse = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/resend-otp",
            new { email = "2026000999@hanu.edu.vn" });

        verifiedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        unknownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await verifiedResponse.Content.ReadAsStringAsync())
            .Should().Be(await unknownResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoginFailuresAcrossDifferentIps_ShouldLockOnlyTheTargetAccount()
    {
        const string targetEmail = "2026000016@hanu.edu.vn";
        const string otherEmail = "2026000017@hanu.edu.vn";
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, targetEmail);
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, otherEmail);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email = targetEmail,
                    password = "WrongPassword1!"
                })
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{attempt}");
            using var response = await _fixture.Client.SendAsync(request);
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests);
        }

        using var lockedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = targetEmail,
                password = AuthTestClient.ValidPassword
            })
        };
        lockedRequest.Headers.Add("X-Forwarded-For", "203.0.113.200");
        using var lockedResponse = await _fixture.Client.SendAsync(lockedRequest);
        lockedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        using var unaffectedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = otherEmail,
                password = AuthTestClient.ValidPassword
            })
        };
        unaffectedRequest.Headers.Add("X-Forwarded-For", "203.0.113.200");
        using var unaffectedResponse = await _fixture.Client.SendAsync(unaffectedRequest);
        unaffectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SendCookieOnlyRequestAsync(string path, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        return _fixture.Client.SendAsync(request);
    }

    private static Task<long> DeleteCurrentOtpAsync(IDatabase redis, string email)
        => redis.KeyDeleteAsync(new RedisKey[]
        {
            $"auth:otp:{email}",
            $"auth:otp:attempts:{email}",
            $"auth:otp:cooldown:{email}"
        });
}
