using System.Net;
using System.Net.Http.Json;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginRateLimitSpecifications
{
    private readonly CodePathApiFixture _fixture;

    public LoginRateLimitSpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ThreeHundredDifferentEmailsBehindOneNat_ShouldNotHitIpRateLimit()
    {
        const string sharedNatIp = "198.51.100.77";

        for (var index = 0; index < 300; index++)
        {
            using var request = CreateInvalidLoginRequest(index, sharedNatIp);
            using var response = await _fixture.Client.SendAsync(request);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task RequestBeyondBurstCapacity_ShouldReturnRetryAfter()
    {
        const string clientIp = "198.51.100.78";

        for (var index = 0; index < 400; index++)
        {
            using var request = CreateInvalidLoginRequest(index, clientIp);
            using var response = await _fixture.Client.SendAsync(request);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using var rejectedRequest = CreateInvalidLoginRequest(401, clientIp);
        using var rejected = await _fixture.Client.SendAsync(rejectedRequest);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
    }

    private static HttpRequestMessage CreateInvalidLoginRequest(int index, string ipAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = $"rate-limit-{index}@example.edu",
                password = string.Empty
            })
        };
        request.Headers.Add("X-Forwarded-For", ipAddress);
        return request;
    }
}
