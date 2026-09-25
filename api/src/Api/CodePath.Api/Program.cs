using System.Text;
using System.Threading.RateLimiting;
using CodePath.Api.Endpoints;
using CodePath.Application;
using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Authorization;
using CodePath.Shared.Kernel.Enums;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSharedInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["Jwt:SigningKey"] 
    ?? "REPLACE_THIS_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_64_CHARS_DEFAULT_DEV_KEY";
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
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000", "http://localhost:5173" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("login-rate-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"login:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAppExceptionHandling();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

var proxySubnet = builder.Configuration["ReverseProxy:KnownNetwork"] ?? "172.16.0.0/12";
var subnetParts = proxySubnet.Split('/');
if (subnetParts.Length == 2 && System.Net.IPAddress.TryParse(subnetParts[0], out var ip) && int.TryParse(subnetParts[1], out var prefix))
{
    forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(ip, prefix));
}
else
{
    forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
    forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
}

app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodePath API v1"));
}

app.UseHttpsRedirection();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "CodePath API", status = "healthy" }))
   .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();

app.MapGet("/health/redis", async (IConnectionMultiplexer redis) =>
{
    if (!redis.IsConnected)
    {
        return Results.Problem(
            detail: "Cannot connect to Redis server.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var db = redis.GetDatabase();
    var testKey = "codepath:healthcheck";
    var testValue = $"ping_{Guid.NewGuid():N}";

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(60));
    var retrievedValue = await db.StringGetAsync(testKey);
    stopwatch.Stop();
    await db.KeyDeleteAsync(testKey);

    var isRoundTripSuccess = (retrievedValue == testValue);
    return isRoundTripSuccess
        ? Results.Ok(new
        {
            service = "Redis",
            status = "healthy",
            roundTripSuccess = true,
            latencyMs = stopwatch.ElapsedMilliseconds,
            endpoint = redis.GetEndPoints().FirstOrDefault()?.ToString()
        })
        : Results.Problem(
            detail: "Redis round-trip verification failed (value mismatch).",
            statusCode: StatusCodes.Status500InternalServerError);
})
.WithName("CheckRedisHealth")
.WithTags("Health");

app.MapAuthEndpoints();

app.Run();

public partial class Program { }
