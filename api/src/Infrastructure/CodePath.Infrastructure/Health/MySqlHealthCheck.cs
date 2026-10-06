using CodePath.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace CodePath.Infrastructure.Health;

public sealed class MySqlHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MySqlHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("MySQL is reachable.")
                : HealthCheckResult.Unhealthy("MySQL rejected the health query.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MySQL health check failed.", exception);
        }
    }
}
