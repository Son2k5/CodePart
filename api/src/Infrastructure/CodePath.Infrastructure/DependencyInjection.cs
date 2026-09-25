using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Infrastructure.Auth.Authorization;
using CodePath.Infrastructure.Auth.Persistence;
using CodePath.Infrastructure.Auth.Services;
using CodePath.Infrastructure.Users.Persistence;
using CodePath.Shared.Kernel.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthInfrastructure(configuration);
        services.AddUsersInfrastructure(configuration);
        return services;
    }

    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Connection string 'Database' was not found in configuration.");

        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)));

        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AuthDbContext>());
        services.AddScoped<IDatabaseMigrator, AuthDbContextMigrator>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, RedisOtpService>();
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();

        return services;
    }

    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException("Connection string 'Database' was not found in configuration.");

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema)));

        services.AddScoped<IUsersDbContext>(sp => sp.GetRequiredService<UsersDbContext>());
        services.AddScoped<IDatabaseMigrator, UsersDbContextMigrator>();

        return services;
    }
}
