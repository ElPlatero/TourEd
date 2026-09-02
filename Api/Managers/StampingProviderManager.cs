using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public sealed class StampingProviderManager
{
    private readonly TouredRepository _repository;

    public StampingProviderManager(TouredRepository repository)
    {
        _repository = repository;
    }

    public Task<List<StampingProvider>> GetStampingProvidersAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => _repository.GetStampingProvidersForUserAsync(userId, cancellationToken);

    public Task<StampingProviderCatalogResult> GetStampingProvidersCatalogAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => _repository.GetStampingProvidersCatalogAsync(userId, cancellationToken);

    public Task<(StampingProvider Provider, List<StampingPoint> Points)?> GetPublicProviderDataAsync(
        string providerSlug,
        int userId,
        CancellationToken cancellationToken = default)
        => _repository.GetPublicProviderDataAsync(providerSlug, userId, cancellationToken);
}
