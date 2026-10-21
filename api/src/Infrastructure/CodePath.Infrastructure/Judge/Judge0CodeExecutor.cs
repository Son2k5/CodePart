using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodePath.Application.Judge;
using CodePath.Domain.Judge;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Judge;

public sealed class JudgeOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:2358/";
    public string ApiToken { get; set; } = "";
    public int Concurrency { get; set; } = 2;
    public int JobTimeoutSeconds { get; set; } = 600;
    public int ExecutionTimeoutSeconds { get; set; } = 45;
    public Dictionary<string, int> Languages { get; set; } = new() { ["cpp"] = 54, ["python"] = 71, ["java"] = 62, ["csharp"] = 51 };
}
public sealed class Judge0CodeExecutor(HttpClient http, IOptions<JudgeOptions> options) : ICodeExecutor
{
    public bool Supports(string language) => options.Value.Languages.ContainsKey(language);
    public async Task<ExecutionResult> Execute(string language, string sourceCode, string stdin, Limits limits, CancellationToken ct)
    {
        if (!options.Value.Languages.TryGetValue(language, out var languageId) || Encoding.UTF8.GetByteCount(sourceCode) > JudgeRules.SourceBytes || Encoding.UTF8.GetByteCount(stdin) > JudgeRules.IoBytes)
            return new(Verdict.InternalError, null, "Invalid execution request.", 0, 0);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.ExecutionTimeoutSeconds));
        static string Encode(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        try
        {
            using var response = await http.PostAsJsonAsync("submissions?base64_encoded=true&wait=false", new {
                source_code = Encode(sourceCode), language_id = languageId, stdin = Encode(stdin),
                cpu_time_limit = limits.TimeLimitMs / 1000d, wall_time_limit = limits.TimeLimitMs / 1000d * 2 + 1,
                memory_limit = limits.MemoryLimitMb * 1024, max_file_size = 1024, max_processes_and_or_threads = 64,
                enable_network = false, enable_per_process_and_thread_time_limit = false,
                enable_per_process_and_thread_memory_limit = false, number_of_runs = 1
            }, deadline.Token);
            response.EnsureSuccessStatusCode();
            using var created = await ReadBounded(response, deadline.Token);
            var token = created.RootElement.GetProperty("token").GetString();
            if (!Guid.TryParse(token, out _)) throw new JsonException("Invalid execution token.");
            while (true)
            {
                await Task.Delay(250, deadline.Token);
                using var poll = await http.GetAsync($"submissions/{token}?base64_encoded=true&fields=status,stdout,stderr,compile_output,time,memory", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                poll.EnsureSuccessStatusCode();
                using var doc = await ReadBounded(poll, deadline.Token);
                var r = doc.RootElement;
                var status = r.GetProperty("status").GetProperty("id").GetInt32();
                if (status is 1 or 2) continue;
                string? Decode(string name)
                {
                    if (!r.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
                    var bytes = Convert.FromBase64String(value.GetString() ?? "");
                    if (bytes.Length > JudgeRules.IoBytes) throw new InvalidDataException("Output limit exceeded.");
                    return Encoding.UTF8.GetString(bytes);
                }
                var output = Decode("stdout");
                var error = status == 6 ? Decode("compile_output") : Decode("stderr");
                var memory = r.TryGetProperty("memory", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0;
                var runtime = r.TryGetProperty("time", out var t) && double.TryParse(t.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? (int)Math.Ceiling(seconds * 1000) : 0;
                var verdict = status switch { 3 => Verdict.Accepted, 4 => Verdict.WrongAnswer, 5 => Verdict.TimeLimitExceeded, 6 => Verdict.CompileError, >= 7 and <= 12 => Verdict.RuntimeError, _ => Verdict.InternalError };
                if (memory > limits.MemoryLimitMb * 1024) verdict = Verdict.MemoryLimitExceeded;
                return new(verdict, output, JudgeSanitizer.Sanitize(error), runtime, memory);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(Verdict.InternalError, null, "Execution service timed out.", 0, 0); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or InvalidDataException)
        { return new(ex is InvalidDataException ? Verdict.RuntimeError : Verdict.InternalError, null, "Execution failed or output limit exceeded.", 0, 0); }
    }
    private static async Task<JsonDocument> ReadBounded(HttpResponseMessage response, CancellationToken ct)
    {
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > 5 * JudgeRules.IoBytes) throw new InvalidDataException("Response too large.");
            buffer.Write(chunk, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}
