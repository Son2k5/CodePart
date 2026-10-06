using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Redis;

/// <summary>
/// Đăng ký IConnectionMultiplexer. Infra owns Redis connection (moved from Shared.Web).
/// </summary>
public static class RedisRegistration
{
    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<RedisConnectionOptions>()
            .Bind(configuration.GetSection(RedisConnectionOptions.SectionName))
            .Validate(options => options.ConnectRetry is >= 0 and <= 20, "Redis ConnectRetry is invalid.")
            .Validate(options => options.ConnectTimeoutMilliseconds is >= 100 and <= 120000, "Redis ConnectTimeoutMilliseconds is invalid.")
            .Validate(options => options.SyncTimeoutMilliseconds is >= 100 and <= 120000, "Redis SyncTimeoutMilliseconds is invalid.")
            .Validate(options => options.KeepAliveSeconds is >= 1 and <= 3600, "Redis KeepAliveSeconds is invalid.")
            .ValidateOnStart();

        var redisOptions = configuration.GetSection(RedisConnectionOptions.SectionName)
            .Get<RedisConnectionOptions>() ?? new RedisConnectionOptions();
        var connectionString = configuration.GetConnectionString("Redis");
        ConfigurationOptions configurationOptions;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var password = configuration["Redis:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Redis or the file secret Redis__Password must be configured.");
            }

            configurationOptions = new ConfigurationOptions
            {
                EndPoints = { configuration["Redis:Host"] ?? "redis" },
                Password = password
            };
        }
        else
        {
            configurationOptions = ConfigurationOptions.Parse(connectionString);
        }

        if (!environment.IsDevelopment()
            && !environment.IsEnvironment("Testing")
            && string.IsNullOrWhiteSpace(configurationOptions.Password))
        {
            throw new InvalidOperationException(
                "Redis authentication is required outside the Development environment.");
        }

        configurationOptions.AbortOnConnectFail = false;
        configurationOptions.ConnectRetry = redisOptions.ConnectRetry;
        configurationOptions.ConnectTimeout = redisOptions.ConnectTimeoutMilliseconds;
        configurationOptions.SyncTimeout = redisOptions.SyncTimeoutMilliseconds;
        configurationOptions.KeepAlive = redisOptions.KeepAliveSeconds;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configurationOptions));

        return services;
    }
}
