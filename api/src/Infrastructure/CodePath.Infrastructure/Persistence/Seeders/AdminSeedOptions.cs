using Microsoft.Extensions.Configuration;

namespace CodePath.Infrastructure.Persistence.Seeders;

/// <summary>
/// Strongly-typed Admin seed config. Bound từ env/section, tránh đọc IConfiguration rời rạc trong Seeder.
/// Ưu tiên biến môi trường ADMIN01_* / ADMIN02_*, fallback section AdminSeed:*.
/// </summary>
public sealed class AdminSeedOptions
{
    public string? Admin01Password { get; init; }
    public string Admin01Email { get; init; } = default!;
    public string Admin01FullName { get; init; } = default!;

    public string? Admin02Password { get; init; }
    public string Admin02Email { get; init; } = default!;
    public string Admin02FullName { get; init; } = default!;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Admin01Password) &&
        !string.IsNullOrWhiteSpace(Admin02Password);

    public static AdminSeedOptions FromConfiguration(IConfiguration config)
    {
        static string Get(IConfiguration c, string envKey, string sectionKey, string fallback)
            => (c[envKey] ?? c[sectionKey] ?? fallback).Trim();

        return new AdminSeedOptions
        {
            Admin01Password = config["ADMIN01_PASSWORD"] ?? config["AdminSeed:Admin01:Password"],
            Admin01Email = Get(config, "ADMIN01_EMAIL", "AdminSeed:Admin01:Email", "admin01@hanu.edu.vn").ToLowerInvariant(),
            Admin01FullName = Get(config, "ADMIN01_FULLNAME", "AdminSeed:Admin01:FullName", "System Administrator 01"),
            Admin02Password = config["ADMIN02_PASSWORD"] ?? config["AdminSeed:Admin02:Password"],
            Admin02Email = Get(config, "ADMIN02_EMAIL", "AdminSeed:Admin02:Email", "admin02@hanu.edu.vn").ToLowerInvariant(),
            Admin02FullName = Get(config, "ADMIN02_FULLNAME", "AdminSeed:Admin02:FullName", "System Administrator 02"),
        };
    }
}
