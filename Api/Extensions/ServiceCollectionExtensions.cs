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
        services.TryAddTransient<IImportService<StampingPoint>, StampingPointImportService>();
        services.TryAddTransient<IImportService<HikingTour>, HikingToursImportService>();
        services.TryAddTransient<IImportManager, ImportManager>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.TryAddTransient<IUserService, TouredRepository>();
        services.TryAddTransient<TouredRepository>();
        return services;
    }
}
