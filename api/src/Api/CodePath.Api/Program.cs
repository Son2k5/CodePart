using System.Text;
using System.Threading.RateLimiting;
using CodePath.Api.Authorization;
using CodePath.Api.Endpoints;
using CodePath.Api.Extensions;
using CodePath.Application;
using CodePath.Application.Auth.Abstractions;
using CodePath.Shared.Kernel.Common;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    Env.TraversePath().Load();
    builder.Configuration.AddEnvironmentVariables();
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
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
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

var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 30);
var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 30);
var codeExecutionPermitLimit = builder.Configuration.GetValue("RateLimiting:CodeExecutionPermitLimit", 20);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Quá nhiều yêu cầu",
            Detail = "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn. Vui lòng thử lại sau.",
            Type = "https://httpstatuses.com/429",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "RATE_LIMIT_EXCEEDED";
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

        return RateLimitPartition.GetFixedWindowLimiter($"login:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(5),
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

// Auto-migration via Program API (thay thế Tool CodePath.Migrator độc lập).
// Domain owns constraints qua IEntityTypeConfiguration, Api orchestrate MigrateAsync().
await app.MigrateDatabasesAsync();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

var proxyNetworks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>()
    ?? (builder.Configuration["ReverseProxy:KnownNetwork"] is not null
        ? new[] { builder.Configuration["ReverseProxy:KnownNetwork"]! }
        : Array.Empty<string>());

if (proxyNetworks.Length > 0)
{
    var trustedNetworks = new List<Microsoft.AspNetCore.HttpOverrides.IPNetwork>();

    foreach (var network in proxyNetworks)
    {
        var subnetParts = network.Split('/');
        if (subnetParts.Length != 2
            || !System.Net.IPAddress.TryParse(subnetParts[0], out var ip)
            || !int.TryParse(subnetParts[1], out var prefix))
        {
            throw new InvalidOperationException($"Reverse proxy network '{network}' is not a valid CIDR value.");
        }

        try
        {
            trustedNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(ip, prefix));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidOperationException($"Reverse proxy network '{network}' has an invalid prefix length.", ex);
        }
    }

    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();

    foreach (var trustedNetwork in trustedNetworks)
    {
        forwardedHeadersOptions.KnownNetworks.Add(trustedNetwork);
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);

// CORS đặt trước AppExceptionHandling để mọi response lỗi 4xx/5xx vẫn luôn có CORS headers
app.UseCors();

app.UseAppExceptionHandling();

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
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "CodePath API", status = "healthy" }))
   .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
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
.RequireRateLimiting("health-rate-limit");

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapExerciseEndpoints();

app.Run();

public partial class Program { }
