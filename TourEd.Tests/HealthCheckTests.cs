using Api.Extensions;
using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class HealthCheckTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-health-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ReportsHealthyForMigratedDatabase()
    {
        var configuration = CreateConfiguration();
        await using (var context = new DataContext(configuration))
        {
            await context.Database.MigrateAsync();
        }

        await using var services = CreateServices(configuration);
        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.All(report.Entries.Values, entry => Assert.Equal(HealthStatus.Healthy, entry.Status));
    }

    [Fact]
    public async Task ReportsUnhealthyWhenMigrationsArePending()
    {
        var configuration = CreateConfiguration();
        await using var services = CreateServices(configuration);

        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["migrations"].Status);
    }

    [Fact]
    public async Task ReportsUnhealthyWhenDefaultProviderIsMissing()
    {
        var configuration = CreateConfiguration();
        await using (var context = new DataContext(configuration))
        {
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM StampingProviders WHERE Id = {0};",
                StampingProvider.TouringenId);
        }

        await using var services = CreateServices(configuration);
        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["database"].Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["migrations"].Status);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["default-provider"].Status);
    }

    private IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();

    private static ServiceProvider CreateServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddDbContext<DataContext>();
        services.AddTouredHealthChecks();
        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
