namespace Api.Services;

internal interface IRegistrationNotificationSender
{
    Task SendAsync(
        int newRequestCount,
        int totalPendingRequestCount,
        CancellationToken cancellationToken = default);
}
