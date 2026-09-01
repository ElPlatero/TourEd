using Api.Options;
using Api.Repositories;
using Microsoft.Extensions.Options;

namespace Api.Services;

internal sealed class RegistrationRequestNotificationService(
    IServiceScopeFactory scopeFactory,
    IRegistrationNotificationSender notificationSender,
    IOptions<RegistrationNotificationOptions> options,
    ILogger<RegistrationRequestNotificationService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan NotificationCooldown = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunNotificationSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunNotificationSafelyAsync(stoppingToken);
        }
    }

    internal async Task<bool> RunNotificationSafelyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = options.Value;
            if (!config.Enabled)
            {
                return true;
            }

            if (!config.Validate(out var validationErrors))
            {
                logger.LogWarning(
                    "Registration notifications are enabled but configuration is invalid: {Errors}. The next scheduled run will retry.",
                    string.Join("; ", validationErrors));
                return false;
            }

            var utcNow = timeProvider.GetUtcNow().UtcDateTime;

            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<TouredRepository>();

            var unnotifiedIds = await repository.GetUnnotifiedPendingRegistrationRequestIdsAsync(cancellationToken);
            if (unnotifiedIds.Count == 0)
            {
                return true;
            }

            var latestSentAt = await repository.GetLastRegistrationNotificationSentAtAsync(cancellationToken);
            if (latestSentAt.HasValue && utcNow - latestSentAt.Value < NotificationCooldown)
            {
                return true;
            }

            var totalPendingCount = await repository.CountPendingRegistrationRequestsAsync(cancellationToken);

            await notificationSender.SendAsync(unnotifiedIds.Count, totalPendingCount, cancellationToken);

            var markedCount = await repository.MarkRegistrationRequestsAdminNotifiedAsync(unnotifiedIds, utcNow, cancellationToken);
            logger.LogInformation(
                "Sent admin notification for {NewRequestCount} new registration request(s) ({MarkedCount} marked). Total pending: {TotalPendingCount}.",
                unnotifiedIds.Count,
                markedCount,
                totalPendingCount);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Could not process registration request notifications ({ExceptionType}); the next scheduled run will retry.",
                exception.GetType().Name);
            return false;
        }
    }
}
