using CodePath.Infrastructure.Learning;
using CodePath.Infrastructure.Ai;
using CodePath.Infrastructure.Judge;
using System.Text;
using System.Threading.RateLimiting;
using CodePath.Api.Authorization;
using CodePath.Api.Endpoints;
using CodePath.Api.Extensions;
using CodePath.Api.Options;
using CodePath.Api.Middleware;
using CodePath.Application;
using CodePath.Application.Auth.Abstractions;
using CodePath.Application.Users.Abstractions;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 40 * 1024 * 1024);

if (builder.Environment.IsDevelopment())
{
    Env.TraversePath().Load();
    builder.Configuration.AddEnvironmentVariables();
}

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"];
if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls(configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries));
}

// Docker/Kubernetes secrets are mounted as files. Normalize only trailing line
// endings so secrets created by common CLI tools match the backing services.
const string secretsDirectory = "/run/secrets";
if (Directory.Exists(secretsDirectory))
{
    var fileSecrets = Directory.EnumerateFiles(secretsDirectory)
        .ToDictionary(
            path => Path.GetFileName(path).Replace("__", ":", StringComparison.Ordinal),
            path => (string?)File.ReadAllText(path).TrimEnd('\r', '\n'),
            StringComparer.OrdinalIgnoreCase);
    builder.Configuration.AddInMemoryCollection(fileSecrets);
}

builder.Services.AddSharedInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 64)
{
    throw new InvalidOperationException("Jwt:SigningKey is missing or less than 64 characters in configuration.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CodePath.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CodePath.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti) && await blacklistService.IsBlacklistedAsync(jti))
                {
                    context.Fail("Token has been revoked.");
                    return;
                }

                var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                var securityVersionClaim = context.Principal?.FindFirst("sv")?.Value;
                if (!Guid.TryParse(subject, out var userId)
                    || !long.TryParse(securityVersionClaim, out var tokenSecurityVersion))
                {
                    context.Fail("Token session claims are invalid.");
                    return;
                }

                var usersDbContext = context.HttpContext.RequestServices.GetRequiredService<IUsersDbContext>();
                var user = await usersDbContext.GetByIdReadOnlyAsync(userId, context.HttpContext.RequestAborted);
                if (user is null
                    || user.Status != UserStatus.Active
                    || user.SecurityVersion != tokenSecurityVersion)
                {
                    context.Fail("Token session has been revoked.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Không được phép truy cập",
                    Detail = context.ErrorDescription ?? "Token không hợp lệ, đã hết hạn hoặc bị thu hồi.",
                    Type = "https://httpstatuses.com/401",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["code"] = ErrorCodes.Unauthorized;
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Truy cập bị từ chối",
                    Detail = "Bạn không có quyền truy cập tài nguyên này.",
                    Type = "https://httpstatuses.com/403",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["code"] = ErrorCodes.Forbidden;
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem);
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageClassContent", p => p.RequireAuthenticatedUser().RequireRole("Admin", "Teacher").AddRequirements(new ActiveUserRequirement()));
    options.AddPolicy("JudgeManager", p => p.RequireAuthenticatedUser().RequireRole("Admin", "Teacher").AddRequirements(new ActiveUserRequirement()));
    options.AddPolicy("ActiveUser", policy =>
        policy.Requirements.Add(new ActiveUserRequirement()));

    options.AddPolicy("ActiveStudent", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Student)));

    options.AddPolicy("ActiveTeacher", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Teacher)));

    options.AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new ActiveUserRequirement(UserRole.Admin)));
});
builder.Services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();

builder.Services.AddOptions<AuthRateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(AuthRateLimitingOptions.SectionName))
    .Validate(options => options.BurstCapacity is >= 1 and <= 100000,
        "Auth rate-limit BurstCapacity must be between 1 and 100000.")
    .Validate(options => options.RefillPerMinute is >= 1 and <= 100000,
        "Auth rate-limit RefillPerMinute must be between 1 and 100000.")
    .Validate(options => options.QueueLimit == 0,
        "Auth rate-limit QueueLimit must be zero.")
    .ValidateOnStart();

builder.Services.AddOptions<ReverseProxyTrustOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyTrustOptions.SectionName))
    .Validate(options => options.ForwardLimit is >= 1 and <= 10,
        "ReverseProxy:ForwardLimit must be between 1 and 10.")
    .Validate(options => options.HasValidAddresses(),
        "ReverseProxy contains an invalid KnownNetworks CIDR or KnownProxies IP address.")
    .ValidateOnStart();

var reverseProxyTrust = builder.Configuration
    .GetSection(ReverseProxyTrustOptions.SectionName)
    .Get<ReverseProxyTrustOptions>() ?? new ReverseProxyTrustOptions();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = reverseProxyTrust.ForwardLimit;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var network in reverseProxyTrust.KnownNetworks)
    {
        var parts = network.Split('/', StringSplitOptions.TrimEntries);
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
            System.Net.IPAddress.Parse(parts[0]),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)));
    }

    foreach (var proxy in reverseProxyTrust.KnownProxies)
    {
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
});

var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 30);
var codeExecutionPermitLimit = builder.Configuration.GetValue("RateLimiting:CodeExecutionPermitLimit", 20);
var loginRateLimit = builder.Configuration
    .GetSection(AuthRateLimitingOptions.SectionName)
    .Get<AuthRateLimitingOptions>() ?? new AuthRateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("class-join-code", context => RateLimitPartition.GetSlidingWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Quá nhiều yêu cầu",
            Detail = "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn. Vui lòng thử lại sau.",
            Type = "https://httpstatuses.com/429",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = context.HttpContext.Request.Path.StartsWithSegments("/api/problems") ? "RATE_LIMITED" : "RATE_LIMIT_EXCEEDED";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };

    options.AddPolicy("auth-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("login-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetTokenBucketLimiter($"login:{clientIp}", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = loginRateLimit.BurstCapacity,
            TokensPerPeriod = loginRateLimit.RefillPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = loginRateLimit.QueueLimit
        });
    });

    options.AddPolicy("password-reset-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"password-reset:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0
        });
    });

    options.AddPolicy("health-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"health:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("public-read-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"public-read:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("code-execution-rate-limit", httpContext =>
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"code:{userId}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = codeExecutionPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddRateLimiter(o => { foreach (var rule in new[] { ("judge-run", 10), ("judge-submit", 5) }) o.AddPolicy(rule.Item1, c => RateLimitPartition.GetSlidingWindowLimiter(c.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", _ => JudgeRateLimits.Options(rule.Item1 == "judge-submit"))); });
builder.Services.AddRateLimiter(o => o.AddPolicy("learning-heartbeat", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddRateLimiter(o => o.AddPolicy("learning-write", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddLearning(builder.Configuration);

if (builder.Configuration.GetValue<bool>("Judge:Enabled")) builder.Services.AddCodeJudge(builder.Configuration);
if (builder.Configuration.GetValue<bool>("Ai:Enabled"))
{
    if (!builder.Configuration.GetValue<bool>("Judge:Enabled")) throw new InvalidOperationException("AI requires Judge:Enabled.");
    builder.Services.AddAiAssistant(builder.Configuration);
    builder.Services.AddSignalR();
}

var app = builder.Build();
if (args.Contains("--migrate-judge")) { using var scope = app.Services.CreateScope(); await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(scope.ServiceProvider.GetRequiredService<JudgeDbContext>().Database); return; }


if (args.Contains("--migrate"))
{
    await app.MigrateDatabasesAsync();
    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();

// CORS đặt trước AppExceptionHandling để mọi response lỗi 4xx/5xx vẫn luôn có CORS headers
app.UseCors();

app.UseAppExceptionHandling();

app.Use(async (httpContext, next) =>
{
    if (httpContext.Request.Path.StartsWithSegments("/api/auth"))
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodePath API v1"));
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<CodePath.Api.Middleware.SessionActivityMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "CodePath API", status = "healthy" }))
   .ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString().ToLowerInvariant(),
                    durationMs = entry.Value.Duration.TotalMilliseconds
                })
        });
    }
})
.WithMetadata(new CodePath.Api.Middleware.SkipActivityTrackingAttribute())
.RequireRateLimiting("health-rate-limit");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).WithMetadata(new CodePath.Api.Middleware.SkipActivityTrackingAttribute())
.ExcludeFromDescription();

app.MapGet("/health/redis", async (IRedisHealthProbe probe, IWebHostEnvironment env, CancellationToken ct) =>
{
    var health = await probe.CheckAsync(ct);
    if (!health.IsConnected)
    {
        return Results.Problem(
            detail: env.IsDevelopment() ? (health.Error ?? "Cannot connect to Redis server.") : "Cannot connect to cache service.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return health.RoundTripSuccess
        ? Results.Ok(new
        {
            service = "Redis",
            status = "healthy",
            roundTripSuccess = true,
            latencyMs = health.LatencyMs,
            endpoint = env.IsDevelopment() ? health.Endpoint : "protected"
        })
        : Results.Problem(
            detail: env.IsDevelopment() ? (health.Error ?? "Redis round-trip verification failed (value mismatch).") : "Cache verification failed.",
            statusCode: StatusCodes.Status500InternalServerError);
})
.WithName("CheckRedisHealth")
.WithTags("Health")
.WithMetadata(new CodePath.Api.Middleware.SkipActivityTrackingAttribute())
.RequireRateLimiting("health-rate-limit");

if (builder.Configuration.GetValue<bool>("Ai:Enabled")) app.MapAiEndpoints();
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapExerciseEndpoints();
if (builder.Configuration.GetValue<bool>("Judge:Enabled")) app.MapJudgeEndpoints();
app.MapTeacherEndpoints();
app.MapClassEndpoints();
app.MapLearningEndpoints();

app.Run();

public partial class Program { }
