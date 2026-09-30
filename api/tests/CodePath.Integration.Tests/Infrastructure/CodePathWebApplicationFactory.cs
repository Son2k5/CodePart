using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodePath.Integration.Tests.Infrastructure;

internal sealed class CodePathWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;

    public CodePathWebApplicationFactory(
        string postgresConnectionString,
        string redisConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisConnectionString = redisConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _postgresConnectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Jwt:Issuer"] = "CodePath.IntegrationTests",
                ["Jwt:Audience"] = "CodePath.IntegrationTests.Client",
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-longer-than-sixty-four-characters-only",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Cors:AllowedOrigins:0"] = "https://localhost",
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:LoginPermitLimit"] = "1000"
            });
        });
    }
}
