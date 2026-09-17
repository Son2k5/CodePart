using CodePath.Shared.Web.Contracts;
using CodePath.Shared.Web.Extensions;
using DotNetEnv;
using StackExchange.Redis;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Đăng ký toàn bộ hạ tầng dùng chung (Redis, Swagger, ...)
builder.Services.AddSharedInfrastructure(builder.Configuration);

IModule[] modules = [];

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseAppExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodePath API v1");
    });
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { service = "CodePath API", status = "healthy" }))
   .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();

// Endpoint kiểm tra kết nối Redis round-trip (Set -> Get -> Clean up)
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

    // 1. Set key có TTL 60s
    await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(60));

    // 2. Read key ngược lại
    var retrievedValue = await db.StringGetAsync(testKey);

    stopwatch.Stop();

    // 3. Xoá key test
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

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program { }
