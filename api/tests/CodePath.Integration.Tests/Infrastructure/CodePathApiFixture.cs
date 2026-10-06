using Testcontainers.MySql;
using Testcontainers.Redis;
using Xunit;

namespace CodePath.Integration.Tests.Infrastructure;

public sealed class CodePathApiFixture : IAsyncLifetime
{
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4")
        .WithDatabase("codepath_tests")
        .WithUsername("codepath")
        .WithPassword("codepath-tests")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    internal CodePathWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    internal CodePathWebApplicationFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? configureServices = null) =>
        new(
            GetDatabaseConnectionString(),
            $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},abortConnect=false",
            settings,
            configureServices);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _mysql.StartAsync(),
            _redis.StartAsync());

        var redisConnectionString = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},abortConnect=false";
        SetTestEnvironment("ConnectionStrings__Database", GetDatabaseConnectionString());
        SetTestEnvironment("ConnectionStrings__Redis", redisConnectionString);
        SetTestEnvironment("Jwt__Issuer", "CodePath.IntegrationTests");
        SetTestEnvironment("Jwt__Audience", "CodePath.IntegrationTests.Client");
        SetTestEnvironment(
            "Jwt__SigningKey",
            "integration-test-signing-key-that-is-longer-than-sixty-four-characters-only");
        SetTestEnvironment("Jwt__AccessTokenExpiryMinutes", "15");
        SetTestEnvironment("ADMIN01_PASSWORD", "AdminSeed-Test1!");
        SetTestEnvironment("ADMIN02_PASSWORD", "AdminSeed-Test2!");

        Factory = new CodePathWebApplicationFactory(
            GetDatabaseConnectionString(),
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
            _mysql.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }

    private void SetTestEnvironment(string key, string value)
    {
        _previousEnvironment.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }

    private string GetDatabaseConnectionString() =>
        $"{_mysql.GetConnectionString()};GuidFormat=Binary16;DateTimeKind=Utc";
}
