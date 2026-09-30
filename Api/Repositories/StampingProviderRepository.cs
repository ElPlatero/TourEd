using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

/// <summary>Stamping providers, their series, user entitlements and per-provider point statistics.</summary>
public sealed class StampingProviderRepository
{
    private readonly DataContext _dbContext;

    public StampingProviderRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<StampingProvider>> GetProvidersAsync(CancellationToken cancellationToken = default)
        => _dbContext.StampingProviders.AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Slug)
            .ToListAsync(cancellationToken);

    public Task<StampingProvider?> FindBySlugAsync(string providerSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = providerSlug.Trim().ToLowerInvariant();
        return _dbContext.StampingProviders.AsNoTracking()
            .FirstOrDefaultAsync(provider => provider.Slug.ToLower() == normalizedSlug, cancellationToken);
    }

    public Task<StampingProvider> GetProviderAsync(int providerId, CancellationToken cancellationToken = default)
        => _dbContext.StampingProviders.AsNoTracking()
            .SingleAsync(provider => provider.Id == providerId, cancellationToken);

    public Task<StampingProvider> GetProviderForUpdateAsync(int providerId, CancellationToken cancellationToken = default)
        => _dbContext.StampingProviders.SingleAsync(provider => provider.Id == providerId, cancellationToken);

    /// <summary>Records a completed provider data import; it is persisted with the next save.</summary>
    public void AddImportRecord(int stampingPointsCount, int hikingToursCount)
        => _dbContext.Add(new Import(default, default, stampingPointsCount, hikingToursCount));

    /// <summary>Persists all pending changes of the request's shared <see cref="DataContext"/>.</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>Returns a provider only when its imported data is ready and its provenance is complete.</summary>
    public Task<StampingProvider?> FindPublicDataProviderAsync(string providerSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = providerSlug.Trim().ToLowerInvariant();
        return _dbContext.StampingProviders.AsNoTracking().SingleOrDefaultAsync(
            item => item.Slug.ToLower() == normalizedSlug &&
                    item.IsAnonymousAccessAllowed &&
                    item.DataSourceUri != null &&
                    item.DataSourceAttribution != null &&
                    item.DataLicenseName != null &&
                    item.DataLicenseUri != null &&
                    item.DataSourceRevision != null &&
                    item.DataSourceUpdatedAt != null &&
                    item.DataImportedAt != null,
            cancellationToken);
    }

    public Task<bool> HasAccessAsync(
        int userId,
        int providerId,
        CancellationToken cancellationToken = default)
        => _dbContext.UserStampingProviders.AsNoTracking().AnyAsync(
            access => access.UserId == userId && access.StampingProviderId == providerId,
            cancellationToken);

    public Task<HashSet<int>> GetEnabledProviderIdsAsync(int userId, CancellationToken cancellationToken = default)
        => _dbContext.UserStampingProviders.AsNoTracking()
            .Where(access => access.UserId == userId)
            .Select(access => access.StampingProviderId)
            .ToHashSetAsync(cancellationToken);

    /// <summary>The user's default provider, but only while the user is still entitled to it.</summary>
    public Task<int?> GetEnabledDefaultProviderIdAsync(int userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId &&
                           user.DefaultStampingProviderId != null &&
                           user.StampingProviders.Any(access =>
                               access.StampingProviderId == user.DefaultStampingProviderId))
            .Select(user => user.DefaultStampingProviderId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Counts permanent points (without validity period) per provider.</summary>
    public Task<Dictionary<int, int>> CountPermanentPointsByProviderAsync(CancellationToken cancellationToken = default)
        => _dbContext.StampingPoints.AsNoTracking()
            .Where(point => point.ValidFrom == null && point.ValidUntil == null)
            .GroupBy(point => point.ProviderId)
            .Select(group => new { ProviderId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ProviderId, group => group.Count, cancellationToken);

    /// <summary>Counts the user's visits to permanent points (without validity period) per provider.</summary>
    public Task<Dictionary<int, int>> CountVisitedPermanentPointsByProviderAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => _dbContext.UserVisits.AsNoTracking()
            .Where(visit => visit.UserId == userId)
            .Join(
                _dbContext.StampingPoints.AsNoTracking().Where(point => point.ValidFrom == null && point.ValidUntil == null),
                visit => visit.StampingPointId,
                point => point.Id,
                (visit, point) => point.ProviderId)
            .GroupBy(providerId => providerId)
            .Select(group => new { ProviderId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ProviderId, group => group.Count, cancellationToken);

    public Task<List<StampingSeries>> GetSeriesAsync(CancellationToken cancellationToken = default)
        => _dbContext.StampingSeries.AsNoTracking()
            .OrderBy(series => series.ProviderId)
            .ThenBy(series => series.Slug)
            .ToListAsync(cancellationToken);
}
