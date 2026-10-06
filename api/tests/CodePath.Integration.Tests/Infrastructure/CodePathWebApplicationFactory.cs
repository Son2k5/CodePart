using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace CodePath.Integration.Tests.Infrastructure;

internal sealed class CodePathWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseConnectionString;
    private readonly string _redisConnectionString;
    private readonly IReadOnlyDictionary<string, string?> _additionalSettings;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public CodePathWebApplicationFactory(
        string databaseConnectionString,
        string redisConnectionString,
        IReadOnlyDictionary<string, string?>? additionalSettings = null,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _databaseConnectionString = databaseConnectionString;
        _redisConnectionString = redisConnectionString;
        _additionalSettings = additionalSettings ?? new Dictionary<string, string?>();
        _configureTestServices = configureTestServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("Auth:RateLimiting:BurstCapacity", "400");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _databaseConnectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Jwt:Issuer"] = "CodePath.IntegrationTests",
                ["Jwt:Audience"] = "CodePath.IntegrationTests.Client",
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-longer-than-sixty-four-characters-only",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Cors:AllowedOrigins:0"] = "https://localhost",
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["Auth:RateLimiting:BurstCapacity"] = "400",
                ["Auth:RateLimiting:RefillPerMinute"] = "1000",
                ["Auth:RateLimiting:QueueLimit"] = "0",
                ["ReverseProxy:KnownNetworks:0"] = "127.0.0.0/8"
            };
            foreach (var setting in _additionalSettings)
            {
                settings[setting.Key] = setting.Value;
            }
            configuration.AddInMemoryCollection(settings);
        });

        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }
}
