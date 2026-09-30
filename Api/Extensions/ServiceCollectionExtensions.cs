using Api.Imports;
using Api.Managers;
using Api.Options;
using Api.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

internal static class ServiceCollectionExtensions
{
    private const string ImportUserAgent = "Mozilla/5.0 (compatible; TourEd/1.0; +https://toured-app.de/)";
    private static readonly TimeSpan ImportTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddImportServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .Configure<TouringenWebsiteConfiguration>(configuration.GetSection("touringen"))
            .Configure<HarzerWandernadelConfiguration>(configuration.GetSection("harzerWandernadel"))
            .AddSingleton(provider => provider.GetRequiredService<IOptions<TouringenWebsiteConfiguration>>().Value)
            .AddSingleton(provider => provider.GetRequiredService<IOptions<HarzerWandernadelConfiguration>>().Value);

        services.AddHttpClient<IHtmlParsingService, HtmlParsingService>();
        services.AddHttpClient<ITouringenStampingPointImportService, TouringenStampingPointImportService>(ConfigureImportClient);
        services.AddHttpClient<IHarzerWandernadelImportService, HarzerWandernadelImportService>(ConfigureImportClient);

        services.TryAddTransient<HikingToursImportService>();
        services.TryAddTransient<IImportManager, ImportManager>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.TryAddTransient<UserRepository>();
        services.TryAddTransient<RegistrationRequestRepository>();
        services.TryAddTransient<AdminAuditRepository>();
        services.TryAddTransient<StampingProviderRepository>();
        services.TryAddTransient<StampingPointRepository>();
        services.TryAddTransient<UserVisitRepository>();
        return services;
    }

    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.TryAddTransient<AdminUserManager>();
        services.TryAddTransient<RegistrationManager>();
        services.TryAddTransient<TourDataManager>();
        services.TryAddTransient<StampingProviderManager>();
        return services;
    }

    /// <summary>Registration notifications and data retention run as hosted background services.</summary>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .Configure<RegistrationNotificationOptions>(configuration.GetSection(RegistrationNotificationOptions.SectionName))
            .AddTransient<IRegistrationNotificationSender, SmtpRegistrationNotificationSender>()
            .AddSingleton<RegistrationRequestNotificationService>()
            .AddHostedService(provider => provider.GetRequiredService<RegistrationRequestNotificationService>())
            .AddSingleton<DataRetentionCleanupService>()
            .AddHostedService(provider => provider.GetRequiredService<DataRetentionCleanupService>());
        return services;
    }

    private static void ConfigureImportClient(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ImportUserAgent);
        client.Timeout = ImportTimeout;
    }
}
