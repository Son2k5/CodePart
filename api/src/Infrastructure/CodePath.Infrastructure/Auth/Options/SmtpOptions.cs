using System.ComponentModel.DataAnnotations;

namespace CodePath.Infrastructure.Auth.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public string? Username { get; init; }
    public string? Password { get; init; }

    [Required, EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "CodePath";
    public string SocketOptions { get; init; } = "StartTls";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress)
        && (string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password));
}
