using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Domain.Exercises.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.CodeExecution;

public sealed class Judge0CodeExecutionService : ICodeExecutionService
{
    private static readonly IReadOnlyDictionary<string, int> LanguageIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = 51,
        ["cpp"] = 54,
        ["java"] = 62,
        ["javascript"] = 63,
        ["python"] = 71
    };

    private readonly HttpClient _httpClient;
    private readonly Judge0Options _options;
    private readonly ILogger<Judge0CodeExecutionService> _logger;

    public Judge0CodeExecutionService(
        HttpClient httpClient,
        IOptions<Judge0Options> options,
        ILogger<Judge0CodeExecutionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool Supports(string language) => LanguageIds.ContainsKey(language.Trim());

    public async Task<CodeExecutionOutcome> ExecuteAsync(
        string language,
        string sourceCode,
        string standardInput,
        string? expectedOutput,
        int timeLimitMs,
        int memoryLimitKb,
        CancellationToken cancellationToken)
    {
        if (!LanguageIds.TryGetValue(language.Trim(), out var languageId))
            return new(SubmissionStatus.SystemError, null, "Unsupported language.", null, null);

        var request = new Judge0SubmissionRequest(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceCode)),
            languageId,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(standardInput)),
            expectedOutput is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(expectedOutput)),
            Math.Max(0.1, timeLimitMs / 1000d),
            memoryLimitKb);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "submissions?base64_encoded=true&wait=true")
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(_options.AuthenticationHeader)
            && !string.IsNullOrWhiteSpace(_options.AuthenticationToken))
        {
            message.Headers.TryAddWithoutValidation(_options.AuthenticationHeader, _options.AuthenticationToken);
        }

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Judge0 returned HTTP {StatusCode}.", response.StatusCode);
                return new(SubmissionStatus.SystemError, null, "Code execution service is unavailable.", null, null);
            }

            var result = await response.Content.ReadFromJsonAsync<Judge0SubmissionResponse>(cancellationToken);
            if (result?.Status is null)
                return new(SubmissionStatus.SystemError, null, "Code execution service returned an invalid response.", null, null);

            var stdout = Decode(result.Stdout);
            var error = FirstNotEmpty(Decode(result.CompileOutput), Decode(result.Stderr), Decode(result.Message));
            int? runtimeMs = double.TryParse(result.Time, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? (int)Math.Ceiling(seconds * 1000)
                : null;

            return new(MapStatus(result.Status.Id), stdout, error, runtimeMs, result.Memory);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SubmissionStatus.SystemError, null, "Code execution service timed out.", null, null);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Cannot connect to Judge0.");
            return new(SubmissionStatus.SystemError, null, "Cannot connect to the code execution service.", null, null);
        }
    }

    private static SubmissionStatus MapStatus(int statusId) => statusId switch
    {
        3 => SubmissionStatus.Accepted,
        4 => SubmissionStatus.WrongAnswer,
        5 => SubmissionStatus.TimeLimitExceeded,
        6 => SubmissionStatus.CompilationError,
        >= 7 and <= 12 => SubmissionStatus.RuntimeError,
        _ => SubmissionStatus.SystemError
    };

    private static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return value; }
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record Judge0SubmissionRequest(
        [property: JsonPropertyName("source_code")] string SourceCode,
        [property: JsonPropertyName("language_id")] int LanguageId,
        [property: JsonPropertyName("stdin")] string Stdin,
        [property: JsonPropertyName("expected_output")] string? ExpectedOutput,
        [property: JsonPropertyName("cpu_time_limit")] double CpuTimeLimit,
        [property: JsonPropertyName("memory_limit")] int MemoryLimit);

    private sealed record Judge0SubmissionResponse(
        [property: JsonPropertyName("stdout")] string? Stdout,
        [property: JsonPropertyName("stderr")] string? Stderr,
        [property: JsonPropertyName("compile_output")] string? CompileOutput,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("time")] string? Time,
        [property: JsonPropertyName("memory")] int? Memory,
        [property: JsonPropertyName("status")] Judge0Status? Status);

    private sealed record Judge0Status(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("description")] string Description);
}
