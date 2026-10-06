using CodePath.Application.Admin.Abstractions;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Exercises.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using CodePath.Infrastructure.Auth.Services.Email;
using CodePath.Infrastructure.Auth.Services.LoginProtection;
using CodePath.Infrastructure.Auth.Services.Otp;
using CodePath.Infrastructure.Auth.Services.Passwords;
using CodePath.Infrastructure.Auth.Services.Tokens;
using CodePath.Infrastructure.CodeExecution;
using CodePath.Infrastructure.Health;
using CodePath.Infrastructure.Persistence;
using CodePath.Infrastructure.Redis;
using CodePath.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

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
            ?? configuration["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var password = configuration["Database:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Database or the file secret Database__Password must be configured.");
            }

            connectionString = new MySqlConnectionStringBuilder
            {
                Server = configuration["Database:Host"] ?? "localhost",
                Port = configuration.GetValue<uint?>("Database:Port") ?? 3306,
                Database = configuration["Database:Name"] ?? "codepath",
                UserID = configuration["Database:User"] ?? "codepath",
                Password = password
            }.ConnectionString;
        }

        services.AddOptions<DatabasePoolOptions>()
            .Bind(configuration.GetSection(DatabasePoolOptions.SectionName))
            .Validate(options => !options.MinPoolSize.HasValue || options.MinPoolSize is >= 0 and <= 10000,
                "MySQL MinPoolSize is invalid.")
            .Validate(options => !options.MaxPoolSize.HasValue || options.MaxPoolSize is >= 1 and <= 10000,
                "MySQL MaxPoolSize is invalid.")
            .Validate(options => !options.MinPoolSize.HasValue || !options.MaxPoolSize.HasValue || options.MinPoolSize <= options.MaxPoolSize,
                "MySQL MinPoolSize cannot exceed MaxPoolSize.")
            .Validate(options => !options.ConnectionIdleLifetimeSeconds.HasValue || options.ConnectionIdleLifetimeSeconds is >= 0 and <= 86400,
                "MySQL ConnectionIdleLifetimeSeconds is invalid.")
            .ValidateOnStart();

        var poolOptions = configuration.GetSection(DatabasePoolOptions.SectionName)
            .Get<DatabasePoolOptions>() ?? new DatabasePoolOptions();
        var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionString)
        {
            GuidFormat = MySqlGuidFormat.Binary16,
            DateTimeKind = MySqlDateTimeKind.Utc,
            CharacterSet = "utf8mb4"
        };
        if (poolOptions.MinPoolSize.HasValue)
        {
            connectionStringBuilder.MinimumPoolSize = (uint)poolOptions.MinPoolSize.Value;
        }
        if (poolOptions.MaxPoolSize.HasValue)
        {
            connectionStringBuilder.MaximumPoolSize = (uint)poolOptions.MaxPoolSize.Value;
        }
        if (poolOptions.ConnectionIdleLifetimeSeconds.HasValue)
        {
            connectionStringBuilder.ConnectionIdleTimeout = (uint)poolOptions.ConnectionIdleLifetimeSeconds.Value;
        }
        connectionString = connectionStringBuilder.ConnectionString;

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.Create(8, 4, 0, ServerType.MySql),
                mySqlOptions => mySqlOptions
                    .DefaultDataTypeMappings(mappings => mappings
                        .WithClrBoolean(MySqlBooleanType.TinyInt1)
                        .WithClrDateTime(MySqlDateTimeType.DateTime6))
                    .EnableRetryOnFailure()
                    .MigrationsHistoryTable("__EFMigrationsHistory")));

        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUsersDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IExerciseDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHealthChecks()
            .AddCheck<MySqlHealthCheck>("mysql", tags: ["ready"])
            .AddCheck<RedisDependencyHealthCheck>("redis", tags: ["ready"]);

        services.AddOptions<Judge0Options>()
            .Bind(configuration.GetSection(Judge0Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<ICodeExecutionService, Judge0CodeExecutionService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<Judge0Options>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.MaxResponseContentBufferSize = 1_048_576;
        });

        services.AddAuthInfrastructure(configuration, environment);
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

        services.AddOptions<AuthFrontendOptions>()
            .Bind(configuration.GetSection(AuthFrontendOptions.SectionName))
            .Validate(options =>
                    environment.IsDevelopment()
                    || environment.IsEnvironment("Testing")
                    || options.IsSecureAbsoluteUrl,
                "AuthFrontend:PublicBaseUrl must be an absolute HTTPS URL outside Development/Testing.")
            .ValidateOnStart();

        services.AddOptions<LoginSecurityOptions>()
            .Bind(configuration.GetSection(LoginSecurityOptions.SectionName))
            .Validate(options => options.BackoffThreshold is >= 3 and <= 20, "Login BackoffThreshold must be between 3 and 20.")
            .Validate(options => options.FailureCounterTtlMinutes is >= 1 and <= 1440, "Login failure-counter TTL is invalid.")
            .Validate(options => options.InitialBackoffSeconds is >= 1 and <= 300, "Initial login backoff is invalid.")
            .Validate(options => options.MaxBackoffSeconds >= options.InitialBackoffSeconds && options.MaxBackoffSeconds <= 3600, "Maximum login backoff is invalid.")
            .ValidateOnStart();

        services.AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection(PasswordHashingOptions.SectionName))
            .Validate(options => options.WorkFactor is >= 10 and <= 16,
                "Auth:PasswordHashing:WorkFactor must be between 10 and 16.")
            .ValidateOnStart();

        services.AddOptions<AuthSessionOptions>()
            .Bind(configuration.GetSection(AuthSessionOptions.SectionName))
            .Validate(options => options.RefreshTokenLifetimeDays is >= 7 and <= 30,
                "Refresh token lifetime must be between 7 and 30 days.")
            .Validate(options => options.RefreshTokenAbsoluteLifetimeDays is >= 7 and <= 30,
                "Absolute refresh token lifetime must be between 7 and 30 days.")
            .Validate(options => options.RefreshTokenAbsoluteLifetimeDays >= options.RefreshTokenLifetimeDays,
                "Absolute refresh token lifetime cannot be shorter than the rolling lifetime.")
            .Validate(options => options.CookieSameSite is "Strict" or "Lax" or "None",
                "Auth:Session:CookieSameSite must be Strict, Lax, or None.")
            .Validate(options => options.CleanupIntervalHours is >= 1 and <= 168,
                "Refresh-token cleanup interval must be between 1 and 168 hours.")
            .Validate(options => options.ExpiredTokenRetentionDays is >= 0 and <= 365,
                "Expired-token retention must be between 0 and 365 days.")
            .ValidateOnStart();

        services.AddOptions<LoginConcurrencyOptions>()
            .Bind(configuration.GetSection(LoginConcurrencyOptions.SectionName))
            .Validate(options => options.MaxConcurrentLogins is >= 1 and <= 4096,
                "MaxConcurrentLogins must be between 1 and 4096.")
            .Validate(options => options.QueueLimit is >= 0 and <= 10000,
                "Login QueueLimit must be between 0 and 10000.")
            .Validate(options => options.QueueTimeoutSeconds is >= 1 and <= 120,
                "Login QueueTimeoutSeconds must be between 1 and 120.")
            .Validate(options => options.MaxConcurrentPerIp is >= 1 and <= 128,
                "MaxConcurrentPerIp must be between 1 and 128.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IAuthSessionPolicy, AuthSessionPolicy>();
        services.AddSingleton<ILoginConcurrencyLimiter, LoginConcurrencyLimiter>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, RedisOtpService>();
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddSingleton<InMemoryLoginAttemptService>();
        services.AddSingleton<ILoginAttemptService, RedisLoginAttemptService>();
        services.AddScoped<IPasswordResetRateLimiter, RedisPasswordResetRateLimiter>();
        services.AddSingleton<IRedisHealthProbe, RedisHealthProbe>();
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }

}
