using System.ComponentModel.DataAnnotations;

namespace CodePath.Infrastructure.Auth.Options;

public sealed class AuthFrontendOptions
{
    public const string SectionName = "AuthFrontend";

    [Required, Url]
    public string PublicBaseUrl { get; init; } = string.Empty;

    public bool IsSecureAbsoluteUrl =>
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
