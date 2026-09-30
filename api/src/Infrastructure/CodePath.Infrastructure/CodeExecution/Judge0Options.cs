using System.ComponentModel.DataAnnotations;

namespace CodePath.Infrastructure.CodeExecution;

public sealed class Judge0Options
{
    public const string SectionName = "CodeExecution:Judge0";

    [Required, Url]
    public string BaseUrl { get; init; } = "http://localhost:2358";

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; init; } = 20;

    public string? AuthenticationHeader { get; init; }
    public string? AuthenticationToken { get; init; }
}
