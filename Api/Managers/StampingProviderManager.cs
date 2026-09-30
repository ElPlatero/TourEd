using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public sealed class StampingProviderManager
{
    private readonly StampingProviderRepository _providers;
    private readonly StampingPointRepository _points;

    public StampingProviderManager(StampingProviderRepository providers, StampingPointRepository points)
    {
        _providers = providers;
        _points = points;
    }

    /// <summary>
    /// Resolves which providers a request may see: <c>all</c> means every entitled provider,
    /// a slug requires an entitlement for that provider, and no slug selects the user's
    /// entitled default provider without any fallback.
    /// </summary>
    public async Task<StampingProviderFilter> ResolveFilterAsync(
        string? providerSlug = null,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(providerSlug, "all", StringComparison.OrdinalIgnoreCase))
        {
            return userId is null ? StampingProviderFilter.Anonymous : StampingProviderFilter.ForUser(userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(providerSlug))
        {
            var provider = await _providers.FindBySlugAsync(providerSlug, cancellationToken)
                ?? throw EntityNotFoundException.Create<StampingProvider>(providerSlug);
            if (userId is null && !provider.IsAnonymousAccessAllowed)
            {
                throw new AccessDeniedException("This stamping provider requires authentication.");
            }
            if (userId is not null && !await _providers.HasAccessAsync(userId.Value, provider.Id, cancellationToken))
            {
                throw new AccessDeniedException("This stamping provider is not enabled for the user.");
            }
            return userId is null
                ? StampingProviderFilter.Single(provider.Id)
                : StampingProviderFilter.SingleForUser(provider.Id, userId.Value);
        }

        if (userId != null)
        {
            var defaultProviderId = await _providers.GetEnabledDefaultProviderIdAsync(userId.Value, cancellationToken);
            if (defaultProviderId is not null)
            {
                return StampingProviderFilter.SingleForUser(defaultProviderId.Value, userId.Value);
            }

            throw new AccessDeniedException("The user has no enabled default stamping provider.");
        }

        var anonymousDefaultProvider = await _providers.GetProviderAsync(StampingProvider.TouringenId, cancellationToken);
        if (!anonymousDefaultProvider.IsAnonymousAccessAllowed)
        {
            throw new AccessDeniedException("The default stamping provider requires authentication.");
        }
        return StampingProviderFilter.Single(anonymousDefaultProvider.Id);
    }

    public async Task<StampingProviderCatalogResult> GetStampingProvidersCatalogAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var providers = await _providers.GetProvidersAsync(cancellationToken);
        var enabledProviderIds = await _providers.GetEnabledProviderIdsAsync(userId, cancellationToken);
        var totalPointsByProvider = await _providers.CountPermanentPointsByProviderAsync(cancellationToken);
        var visitedPointsByProvider = await _providers.CountVisitedPermanentPointsByProviderAsync(userId, cancellationToken);

        var providerDtos = providers.Select(provider =>
        {
            var isEnabled = enabledProviderIds.Contains(provider.Id);
            var isDataReady = provider.IsAnonymousAccessAllowed;
            var totalPoints = isDataReady ? totalPointsByProvider.GetValueOrDefault(provider.Id, 0) : (int?)null;
            var visitedPoints = isDataReady ? visitedPointsByProvider.GetValueOrDefault(provider.Id, 0) : (int?)null;
            return StampingProviderDetailsDto.Create(provider, isEnabled, isDataReady, totalPoints, visitedPoints);
        }).ToList();

        var overallTotal = providerDtos
            .Where(dto => dto.IsEnabled && dto.IsDataReady)
            .Sum(dto => dto.TotalPoints ?? 0);

        var overallVisited = providerDtos
            .Where(dto => dto.IsEnabled && dto.IsDataReady)
            .Sum(dto => dto.VisitedPoints ?? 0);

        return new StampingProviderCatalogResult(
            providerDtos.Count,
            overallTotal,
            overallVisited,
            providerDtos);
    }

    public async Task<(StampingProvider Provider, List<StampingPoint> Points)?> GetPublicProviderDataAsync(
        string providerSlug,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.FindPublicDataProviderAsync(providerSlug, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        if (!await _providers.HasAccessAsync(userId, provider.Id, cancellationToken))
        {
            throw new AccessDeniedException("This stamping provider is not enabled for the user.");
        }

        var points = await _points.GetPointsForProviderAsync(provider.Id, cancellationToken);
        return (provider, points);
    }
}
