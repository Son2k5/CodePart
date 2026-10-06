using CodePath.Application.Auth.Abstractions;
using CodePath.Infrastructure.Auth.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodePath.Infrastructure.Auth.Services.Tokens;

public sealed class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retention;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuthSessionOptions> options,
        ILogger<RefreshTokenCleanupService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
        _interval = TimeSpan.FromHours(options.Value.CleanupIntervalHours);
        _retention = TimeSpan.FromDays(options.Value.ExpiredTokenRetentionDays);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IAuthDbContext>();
            var deleted = await dbContext.DeleteExpiredRefreshTokensAsync(
                _timeProvider.GetUtcNow().UtcDateTime.Subtract(_retention),
                cancellationToken);
            var deletedPasswordResetTokens = await dbContext.DeleteExpiredPasswordResetTokensAsync(
                _timeProvider.GetUtcNow().UtcDateTime.Subtract(_retention),
                cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} expired refresh tokens outside the retention window.", deleted);
            }

            if (deletedPasswordResetTokens > 0)
            {
                _logger.LogInformation(
                    "Deleted {Count} expired password reset tokens outside the retention window.",
                    deletedPasswordResetTokens);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to clean up expired refresh tokens.");
        }
    }
}
