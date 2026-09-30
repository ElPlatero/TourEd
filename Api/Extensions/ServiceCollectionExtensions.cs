using Api.Managers;
using Api.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Services;

namespace Api.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        services.TryAddTransient<IHtmlParsingService, HtmlParsingService>();
        services.TryAddTransient<IImportService<HikingTour>, HikingToursImportService>();
        services.TryAddTransient<IImportManager, ImportManager>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddTransient<UserRepository>();
        services.TryAddTransient<IUserService>(provider => provider.GetRequiredService<UserRepository>());
        services.TryAddTransient<RegistrationRequestRepository>();
        services.TryAddTransient<IRegistrationRequestService>(provider => provider.GetRequiredService<RegistrationRequestRepository>());
        services.TryAddTransient<AdminAuditRepository>();
        services.TryAddTransient<StampingProviderRepository>();
        services.TryAddTransient<IGoogleLoginService, GoogleLoginService>();
        services.TryAddTransient<TouredRepository>();
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
