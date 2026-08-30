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

    public Task<List<StampingProvider>> GetStampingProvidersAsync(bool includeRestrictedProviders)
        => _repository.GetStampingProvidersAsync(includeRestrictedProviders);
}
