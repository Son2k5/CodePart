using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using CodePath.Integration.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodePath.Integration.Tests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginTimingSpecifications
{
    private readonly CodePathApiFixture _fixture;

    public LoginTimingSpecifications(CodePathApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MissingEmailAndWrongPassword_ShouldHaveEquivalentResponseAndComparableTiming()
    {
        const string existingEmail = "2026000030@hanu.edu.vn";
        await AuthTestClient.RegisterVerifyAndLoginAsync(_fixture, existingEmail);

        var existingDurations = new List<double>();
        var missingDurations = new List<double>();
        string? expectedBody = null;

        for (var sample = 0; sample < 3; sample++)
        {
            var existing = await MeasureFailureAsync(
                existingEmail,
                $"198.51.100.{150 + sample}");
            var missing = await MeasureFailureAsync(
                $"missing-{sample}@example.edu",
                $"198.51.100.{160 + sample}");

            existing.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            missing.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            expectedBody ??= existing.Body;
            existing.Body.Should().Be(expectedBody);
            missing.Body.Should().Be(expectedBody);
            existingDurations.Add(existing.ElapsedMilliseconds);
            missingDurations.Add(missing.ElapsedMilliseconds);
            existing.Response.Dispose();
            missing.Response.Dispose();
        }

        var existingMedian = Median(existingDurations);
        var missingMedian = Median(missingDurations);
        var ratio = Math.Max(existingMedian, missingMedian) / Math.Max(1, Math.Min(existingMedian, missingMedian));
        ratio.Should().BeLessThan(3);
    }

    private async Task<MeasuredFailure> MeasureFailureAsync(string email, string ipAddress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "WrongPassword1!" })
        };
        request.Headers.Add("X-Forwarded-For", ipAddress);

        var started = Stopwatch.GetTimestamp();
        var response = await _fixture.Client.SendAsync(request);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();
        return new MeasuredFailure(response, body, elapsed);
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private sealed record MeasuredFailure(
        HttpResponseMessage Response,
        string Body,
        double ElapsedMilliseconds);
}
