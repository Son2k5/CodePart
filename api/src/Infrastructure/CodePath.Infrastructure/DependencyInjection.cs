using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services;
using CodePath.Infrastructure.CodeExecution;
using CodePath.Infrastructure.Persistence;
using CodePath.Infrastructure.Redis;
using CodePath.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddRedis(configuration, environment);

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
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IExerciseDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<Judge0Options>()
            .Bind(configuration.GetSection(Judge0Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<ICodeExecutionService, Judge0CodeExecutionService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<Judge0Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });

        services.AddAuthInfrastructure(configuration, environment);
        services.AddUsersInfrastructure(configuration);
        return services;
    }

    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options =>
                    environment.IsDevelopment()
                    || environment.IsEnvironment("Testing")
                    || options.IsConfigured,
                "Production SMTP configuration is incomplete.")
            .ValidateOnStart();

        services.AddOptions<LoginSecurityOptions>()
            .Bind(configuration.GetSection(LoginSecurityOptions.SectionName))
            .Validate(options => options.MaxFailures is >= 3 and <= 20, "Login MaxFailures must be between 3 and 20.")
            .Validate(options => options.FailureWindowMinutes is >= 1 and <= 1440, "Login failure window is invalid.")
            .Validate(options => options.LockoutMinutes is >= 1 and <= 1440, "Login lockout duration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, RedisOtpService>();
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();
        services.AddScoped<IUserStatusCache, RedisUserStatusCache>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<ILoginAttemptService, RedisLoginAttemptService>();
        services.AddSingleton<IRedisHealthProbe, RedisHealthProbe>();
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }

    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
