using System.Net.Http.Json;
using System.Text.Json;
using CodePath.Application.Ai;
namespace CodePath.Infrastructure.Ai;

public sealed class PythonAiServiceClient(HttpClient http) : IAiAssistantClient
{
    public async Task<AiResponse> Assist(JsonElement context, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await http.PostAsJsonAsync("v1/assist", context, deadline.Token);
            response.EnsureSuccessStatusCode();
            var hint = await response.Content.ReadFromJsonAsync<AiResponse>(cancellationToken: deadline.Token);
            if (hint == null || string.IsNullOrWhiteSpace(hint.Message) || hint.Message.Length > 6000 ||
                hint.Stage is not ("Start" or "InProgress" or "LogicError" or "CannotRun" or "PartialPass") ||
                hint.GuardStatus is not ("ok" or "regenerated" or "fallback") || hint.Level is < 1 or > 3 ||
                hint.Level != context.GetProperty("request").GetProperty("level").GetInt32() ||
                hint.Steps == null || hint.Steps.Length > 8 || hint.SuspectedLines == null || hint.SuspectedLines.Length > 10 || hint.StageConfidence is < 0 or > 1)
                throw new JsonException("Invalid AI response");
            return hint;
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or OperationCanceledException or JsonException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            var r = context.GetProperty("runState");
            var verdicts = r.GetProperty("verdicts").EnumerateArray().Select(v => v.GetProperty("verdict").GetString()).ToArray();
            var source = context.GetProperty("sourceCode").GetString()?.Trim() ?? "";
            var starter = context.GetProperty("problem").GetProperty("starterCode").GetString()?.Trim() ?? "";
            var noRun = !r.TryGetProperty("lastAction", out var action) || action.GetString() == "none";
            var hasError = new[] { "compileError", "runtimeError" }.Any(key => r.TryGetProperty(key, out var error) && !string.IsNullOrEmpty(error.GetString()));
            var stage = hasError || verdicts.Any(v => v is "CompileError" or "RuntimeError" or "SyntaxError" or "IOFormat") ? "CannotRun" :
                r.GetProperty("passed").GetInt32() > 0 && r.GetProperty("passed").GetInt32() < r.GetProperty("total").GetInt32() || verdicts.Any(v => v is "TimeLimitExceeded" or "MemoryLimitExceeded") ? "PartialPass" :
                verdicts.Contains("WrongAnswer") ? "LogicError" : noRun && (source == starter || source.Length < 8) ? "Start" : "InProgress";
            return new(stage, 1, "Unknown", "Hãy đọc lại đầu vào, đầu ra và ghi giá trị các biến qua từng bước trên một ví dụ nhỏ.",
                [], [], null, context.GetProperty("request").GetProperty("level").GetInt32(), "fallback", "rule-based", "fallback-v1", 0, 0);
        }
    }
}
