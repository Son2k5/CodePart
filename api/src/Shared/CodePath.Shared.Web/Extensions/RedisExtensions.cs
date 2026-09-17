using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CodePath.Shared.Web.Extensions;

public static class RedisExtensions
{
    /// <summary>
    /// Cấu hình chi tiết kết nối Redis và đăng ký IConnectionMultiplexer Singleton
    /// </summary>
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
