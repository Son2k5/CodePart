using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Infrastructure.Auth.Authorization;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services;
using CodePath.Infrastructure.Persistence;
using CodePath.Infrastructure.Redis;
using CodePath.Shared.Kernel.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRedis(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Connection string 'Database' was not found in configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory")));

        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUsersDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddAuthInfrastructure(configuration);
        services.AddUsersInfrastructure(configuration);
        return services;
    }

    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, RedisOtpService>();
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();
        services.AddScoped<IUserStatusCache, RedisUserStatusCache>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddSingleton<IRedisHealthProbe, RedisHealthProbe>();
        services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();

        return services;
    }

    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
