using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace TourEd.Tests;

public sealed class RegistrationRequestCleanupServiceTests
{
    [Fact]
    public async Task CleanupFailureIsContainedForScheduledRetry()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        await serviceProvider.DisposeAsync();
        var service = new RegistrationRequestCleanupService(
            scopeFactory,
            NullLogger<RegistrationRequestCleanupService>.Instance,
            TimeProvider.System);

        var succeeded = await service.RunCleanupSafelyAsync();

        Assert.False(succeeded);
        Assert.Equal(TimeSpan.FromHours(24), RegistrationRequestCleanupService.Interval);
    }
}
