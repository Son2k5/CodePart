using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CodePath.Infrastructure.Redis;

/// <summary>
/// Đăng ký IConnectionMultiplexer. Infra owns Redis connection (moved from Shared.Web).
/// </summary>
public static class RedisRegistration
{
    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Redis connection string 'ConnectionStrings:Redis' is missing in configuration.");
        }

        var configurationOptions = ConfigurationOptions.Parse(connectionString);
        configurationOptions.AbortOnConnectFail = false;
        configurationOptions.ConnectRetry = 3;
        configurationOptions.ConnectTimeout = 5000;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configurationOptions));

        return services;
    }
}
