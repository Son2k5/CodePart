using System.Text.RegularExpressions;

namespace CodePath.Application.Judge;

public static class JudgeSanitizer
{
    private const int MaxOutputLength = 4000;

    // Matches unix paths like /box/, /tmp/..., /sandbox/..., /usr/..., /var/..., /home/...
    private static readonly Regex UnixPathRegex = new(
        @"(?:/box/|/(?:tmp|sandbox|var|usr|opt|home|etc)(?:/[a-zA-Z0-9_\.\-]+)?/)",
        RegexOptions.Compiled);

    // Matches Windows paths like C:\... or D:\...
    private static readonly Regex WindowsPathRegex = new(
        @"[a-zA-Z]:\\[^:\n\r]+[\\/]",
        RegexOptions.Compiled);

    // Matches container IDs, internal hostnames, runner identifiers, or IPs
    private static readonly Regex HostnameRegex = new(
        @"(?:(?:root|user)@[a-zA-Z0-9_\.\-]+:?|(?:runner|worker|container|node)-[a-zA-Z0-9_\.\-]+|\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b)",
        RegexOptions.Compiled);

    public static string? Sanitize(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var sanitized = output;

        // Replace internal paths with clean relative filenames
        sanitized = UnixPathRegex.Replace(sanitized, string.Empty);
        sanitized = WindowsPathRegex.Replace(sanitized, string.Empty);
        sanitized = HostnameRegex.Replace(sanitized, string.Empty);

        // Limit length to prevent large memory/response DOS
        if (sanitized.Length > MaxOutputLength)
        {
            sanitized = sanitized[..MaxOutputLength] + "\n... [Đã cắt bớt phần thông báo dài]";
        }

        return sanitized.Trim();
    }
}
