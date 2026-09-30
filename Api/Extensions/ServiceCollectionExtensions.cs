using Api.Authentication;
using Api.Entities;
using Api.Imports;
using Api.Managers;
using Api.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        services.TryAddTransient<IHtmlParsingService, HtmlParsingService>();
        services.TryAddTransient<HikingToursImportService>();
        services.TryAddTransient<IImportManager, ImportManager>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddTransient<UserRepository>();
        services.TryAddTransient<RegistrationRequestRepository>();
        services.TryAddTransient<AdminAuditRepository>();
        services.TryAddTransient<StampingProviderRepository>();
        services.TryAddTransient<GoogleLoginService>();
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
}
