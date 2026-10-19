using System.Net;
using System.Text.Json;
using CodePath.Application.Ai;
using CodePath.Infrastructure.Ai;
using Xunit;
namespace CodePath.Unit.Tests.Ai;

public sealed class AiAssistantTests
{
    [Theory]
    [InlineData(1, 0, 0, 0, true)]
    [InlineData(2, 0, 9, 9, false)]
    [InlineData(3, 1, 9, 9, false)]
    [InlineData(2, 1, 1, 2, false)]
    [InlineData(2, 1, 2, 0, true)]
    [InlineData(3, 2, 0, 3, true)]
    [InlineData(4, 3, 9, 9, false)]
    public void SequentialUnlock(int requested, int previous, int failures, double minutes, bool expected)
        => Assert.Equal(expected, AiPolicy.CanUnlock(requested, previous, failures, minutes));
    [Theory]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(31, false)]
    public void QuotaBoundary(int consumed, bool expected) => Assert.Equal(expected, AiPolicy.HasQuota(consumed));
    private static JsonElement Context => JsonSerializer.SerializeToElement(new {
        sourceCode = "print(1)", problem = new { starterCode = "" }, request = new { level = 2 },
        runState = new { passed = 8, total = 15, verdicts = new[] { new { verdict = "WrongAnswer" } } }
    });
    [Theory]
    [InlineData(503, "")]
    [InlineData(200, "not-json")]
    [InlineData(200, "{}")]
    public async Task UnavailableOrMalformedUsesFallback(int status, string body)
    {
        using var http = new HttpClient(new Handler(status, body)) { BaseAddress = new Uri("http://ai/") };
        var result = await new PythonAiServiceClient(http).Assist(Context, default);
        Assert.Equal("fallback", result.GuardStatus); Assert.Equal("PartialPass", result.Stage); Assert.Equal(2, result.Level);
    }
    [Fact]
    public async Task CallerCancellationIsNotSwallowed()
    {
        using var http = new HttpClient(new Handler(503, "")) { BaseAddress = new Uri("http://ai/") };
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PythonAiServiceClient(http).Assist(Context, cancellation.Token));
    }
    private sealed class Handler(int status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) }); }
    }
}
