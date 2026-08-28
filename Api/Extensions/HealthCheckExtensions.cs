using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TourEd.Lib.Abstractions.Models;

namespace Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddTouredHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<DataContext>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"])
            .AddDbContextCheck<DataContext>(
                "migrations",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                customTestQuery: HasNoPendingMigrationsAsync)
            .AddDbContextCheck<DataContext>(
                "default-provider",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                customTestQuery: HasDefaultProviderAsync);

        return services;
    }

    private static async Task<bool> HasNoPendingMigrationsAsync(
        DataContext context,
        CancellationToken cancellationToken)
        => !(await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();

    private static Task<bool> HasDefaultProviderAsync(
        DataContext context,
        CancellationToken cancellationToken)
        => context.StampingProviders.AsNoTracking().AnyAsync(
            provider => provider.Id == StampingProvider.TouringenId &&
                        provider.Slug == StampingProvider.TouringenSlug,
            cancellationToken);
}
