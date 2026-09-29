using System.ComponentModel.DataAnnotations;

namespace CodePath.Infrastructure.Auth.Options;

/// <summary>
/// Strongly-typed Jwt configuration. Bound from section "Jwt", validated on start (fail-fast).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    [MinLength(64, ErrorMessage = "Jwt:SigningKey must be at least 64 characters.")]
    public string SigningKey { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "CodePath.Api";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "CodePath.Client";

    [Range(1, 60 * 24)]
    public int AccessTokenExpiryMinutes { get; set; } = 15;
}
