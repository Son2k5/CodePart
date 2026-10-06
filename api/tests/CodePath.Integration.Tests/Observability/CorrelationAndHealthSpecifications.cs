using System.Net;
using System.Text.Json;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodePath.Integration.Tests.Observability;

[Collection(IntegrationTestCollection.Name)]
public sealed class CorrelationAndHealthSpecifications
{
    private readonly CodePathApiFixture _fixture;

    public CorrelationAndHealthSpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CorrelationId_ShouldBeReturnedAndPreserved()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Correlation-ID", "capacity-test-123");

        using var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-ID").Should().ContainSingle("capacity-test-123");
    }

    [Fact]
    public async Task ReadinessHealth_ShouldIncludeMySqlAndRedis()
    {
        using var response = await _fixture.Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = json.RootElement.GetProperty("checks");
        checks.GetProperty("mysql").GetProperty("status").GetString().Should().Be("healthy");
        checks.GetProperty("redis").GetProperty("status").GetString().Should().Be("healthy");
    }
}
