using System.Net;
using System.Text;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using CodePath.Infrastructure.Judge;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodePath.Unit.Tests.Judge;
public class JudgeExecutorTests
{
    [Fact]
    public async Task Executor_enforces_sandbox_limits_and_polls_until_finished()
    {
        var handler = new StubHandler();
        using var http = new HttpClient(handler) { BaseAddress = new("http://judge.invalid/") };
        var executor = new Judge0CodeExecutor(http, Options.Create(new JudgeOptions()));
        var result = await executor.Execute("python", "print(42)", "input", new(1000, 64), CancellationToken.None);
        Assert.Equal(Verdict.Accepted, result.Verdict); Assert.Equal("42\n", result.Output); Assert.Equal(20, result.RuntimeMs);
        Assert.Contains("\"enable_network\":false", handler.Request);
        Assert.Contains("\"max_file_size\":1024", handler.Request);
        Assert.Contains("\"max_processes_and_or_threads\":64", handler.Request);
        Assert.DoesNotContain("expected_output", handler.Request);
    }
    [Fact]
    public async Task Invalid_language_and_oversized_input_never_reach_executor()
    {
        var handler = new StubHandler(); using var http = new HttpClient(handler) { BaseAddress = new("http://judge.invalid/") };
        var executor = new Judge0CodeExecutor(http, Options.Create(new JudgeOptions()));
        Assert.Equal(Verdict.InternalError, (await executor.Execute("unknown", "code", "", new(1000, 64), default)).Verdict);
        Assert.Equal(Verdict.InternalError, (await executor.Execute("python", "code", new string('x', JudgeRules.IoBytes + 1), new(1000, 64), default)).Verdict);
        Assert.Null(handler.Request);
    }
    private sealed class StubHandler : HttpMessageHandler
    {
        public string? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = "{\"status\":{\"id\":3},\"stdout\":\"NDIK\",\"stderr\":null,\"time\":\"0.02\",\"memory\":1024}";
            if (request.Method == HttpMethod.Post)
            { Request = await request.Content!.ReadAsStringAsync(ct); json = "{\"token\":\"" + Guid.NewGuid() + "\"}"; }
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
