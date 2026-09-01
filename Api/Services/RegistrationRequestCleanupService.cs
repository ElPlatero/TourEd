using Api.Repositories;

namespace Api.Services;

internal sealed class RegistrationRequestCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RegistrationRequestCleanupService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupSafelyAsync(stoppingToken);
        }
    }

    internal async Task<bool> RunCleanupSafelyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<TouredRepository>();
            var deletedCount = await repository.CleanupExpiredRegistrationRequestsAsync(
                timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);
            if (deletedCount > 0)
            {
                logger.LogInformation("Deleted {DeletedCount} expired registration requests.", deletedCount);
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not clean up expired registration requests; the next scheduled run will retry.");
            return false;
        }
    }
}
