using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace CodePath.Integration.Tests.Infrastructure;

public sealed class CodePathApiFixture : IAsyncLifetime
{
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("codepath_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    internal CodePathWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync());

        var redisConnectionString = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},abortConnect=false";
        SetTestEnvironment("ConnectionStrings__Database", _postgres.GetConnectionString());
        SetTestEnvironment("ConnectionStrings__Redis", redisConnectionString);
        SetTestEnvironment("Jwt__Issuer", "CodePath.IntegrationTests");
        SetTestEnvironment("Jwt__Audience", "CodePath.IntegrationTests.Client");
        SetTestEnvironment(
            "Jwt__SigningKey",
            "integration-test-signing-key-that-is-longer-than-sixty-four-characters-only");
        SetTestEnvironment("Jwt__AccessTokenExpiryMinutes", "15");

        Factory = new CodePathWebApplicationFactory(
            _postgres.GetConnectionString(),
            redisConnectionString);

        Client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        Factory?.Dispose();

        foreach (var (key, value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }

    private void SetTestEnvironment(string key, string value)
    {
        _previousEnvironment.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }
}
