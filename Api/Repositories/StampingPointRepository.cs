using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

/// <summary>Stamping points and the hiking tours that connect them.</summary>
public sealed class StampingPointRepository
{
    private readonly DataContext _dbContext;

    public StampingPointRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<StampingPoint>> GetPointsForProviderAsync(int providerId, CancellationToken cancellationToken = default)
        => _dbContext.StampingPoints.AsNoTracking()
            .Where(point => point.ProviderId == providerId)
            .OrderBy(point => point.Number)
            .ToListAsync(cancellationToken);

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? visit)>> GetStampingPointsAsync(string? nameFilter = null, (Position Centre, decimal Radius)? geoFilter = null, StampingProviderFilter? providerFilter = null, string? seriesSlug = null, int? userId = null, bool? excludeVisited = null, params int[] stampingPointsNr)
    {
        IQueryable<StampingPoint> query = _dbContext.StampingPoints.AsNoTracking().Include(point => point.Series);
        if (providerFilter?.UserId is { } permittedUserId)
        {
            query = query.Where(point => _dbContext.UserStampingProviders.Any(access =>
                access.UserId == permittedUserId && access.StampingProviderId == point.ProviderId));
        }
        else if (providerFilter is { IsAnonymousOnly: true })
        {
            query = query.Where(point => point.Provider.IsAnonymousAccessAllowed);
        }
        if (providerFilter is { IncludesAllProviders: false, ProviderId: { } providerId })
        {
            query = query.Where(p => p.ProviderId == providerId);
        }
        if (!string.IsNullOrWhiteSpace(seriesSlug))
        {
            var normalizedSeriesSlug = seriesSlug.Trim().ToLowerInvariant();
            query = query.Where(point => point.Series.Slug.ToLower() == normalizedSeriesSlug);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.Trim().ToLowerInvariant()));
        }

        if (stampingPointsNr.Length > 0)
        {
            query = query.Where(p => p.Number.HasValue && stampingPointsNr.Contains(p.Number.Value));
        }

        var result = from point in query
            join rawTourPoint in _dbContext.StampingPointsInTours.Include(p => p.Tour).ThenInclude(p => p.StampingPoints).ThenInclude(p => p.StampingPoint) on point.Id equals rawTourPoint.StampingPointId into joinedTourPoints
            from tourPoint in joinedTourPoints.DefaultIfEmpty()
            group tourPoint by point into groupedTours
            select new { Point = groupedTours.Key, UserVisit = userId == null ? null : _dbContext.UserVisits.FirstOrDefault(p => p.StampingPointId == groupedTours.Key.Id && p.UserId == userId), Tours = groupedTours.Select(p => p.Tour).ToList() };

        if (excludeVisited != null && userId != null)
        {
            result = excludeVisited.Value 
                ? result.Where(p => _dbContext.UserVisits.Where(q => q.UserId == userId.Value).All(q => q.StampingPointId != p.Point.Id)) 
                : result.Where(p => _dbContext.UserVisits.Where(q => q.UserId == userId.Value).Any(q => q.StampingPointId == p.Point.Id));
        }
        
        var dto = await result.ToListAsync();
        if (geoFilter != null)
        {
            dto = dto.Where(p => Position.GetDistance(p.Point.Position, geoFilter.Value.Centre) < geoFilter.Value.Radius).ToList();
        }
        var providers = await GetStampingProvidersAsync(dto.Select(p => p.Point.ProviderId));
        var series = await GetStampingSeriesAsync(dto.Select(p => p.Point.SeriesId));
        return dto.Select(p =>
            (p.Point with { Provider = providers[p.Point.ProviderId], Series = series[p.Point.SeriesId] },
                p.Tours.Any(q => q != null) ? p.Tours : null,
                (UserVisit?) p.UserVisit)).ToList();
    }

    public async Task<StampingPoint> GetStampingPointAsync(int stampingPointNumber, StampingProviderFilter providerFilter, string? seriesSlug = null)
    {
        if (providerFilter.IncludesAllProviders)
        {
            throw new RequestValidationException("A single stamping point lookup requires one provider.");
        }

        var normalizedSeriesSlug = seriesSlug?.Trim().ToLowerInvariant();
        var query = _dbContext.StampingPoints.Include(p => p.Provider).Include(p => p.Series)
            .Where(p => p.Number == stampingPointNumber && p.ProviderId == providerFilter.ProviderId);
        if (!string.IsNullOrWhiteSpace(normalizedSeriesSlug))
        {
            query = query.Where(point => point.Series.Slug.ToLower() == normalizedSeriesSlug);
        }
        else
        {
            query = query.Where(point => point.Series.Slug == StampingSeries.TouringenStandardSlug);
        }

        return await query.FirstOrDefaultAsync()
               ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointNumber);
    }

    public async Task<StampingPoint> GetStampingPointByIdAsync(int stampingPointId, StampingProviderFilter providerFilter)
    {
        if (providerFilter.IncludesAllProviders)
        {
            throw new RequestValidationException("A single stamping point lookup requires one provider.");
        }

        return await _dbContext.StampingPoints.Include(point => point.Provider).Include(point => point.Series)
                   .FirstOrDefaultAsync(point => point.Id == stampingPointId && point.ProviderId == providerFilter.ProviderId)
               ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointId);
    }

    public async Task<IReadOnlyList<StampingPoint>> SaveStampingPointsAsync(params StampingPoint[] points)
    {
        if (points.Length == 0)
        {
            return Array.Empty<StampingPoint>();
        }

        var importedNumberedPoints = points
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => (point.SeriesId, Number: point.Number!.Value));
        var importedUnnumberedPoints = points
            .Where(point => !point.Number.HasValue)
            .ToDictionary(point => (point.ProviderId, point.ExternalId));
        _ = points.ToDictionary(point => (point.ProviderId, point.ExternalId));
        var seriesIds = points.Select(point => point.SeriesId).Distinct().ToArray();
        var providerIds = points.Select(point => point.ProviderId).Distinct().ToArray();
        var existingPoints = await _dbContext.StampingPoints.AsNoTracking()
            .Where(point => seriesIds.Contains(point.SeriesId) || providerIds.Contains(point.ProviderId))
            .ToArrayAsync();
        var existingNumberedPoints = existingPoints
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => (point.SeriesId, Number: point.Number!.Value));
        var existingPointsByExternalId = existingPoints
            .ToDictionary(point => (point.ProviderId, point.ExternalId));
        var savedPoints = new List<StampingPoint>(points.Length);

        foreach (var (key, importedPoint) in importedNumberedPoints)
        {
            var pointToSave = (existingNumberedPoints.TryGetValue(key, out var existingPoint) ||
                               existingPointsByExternalId.TryGetValue((importedPoint.ProviderId, importedPoint.ExternalId), out existingPoint))
                ? importedPoint with { Id = existingPoint.Id }
                : importedPoint with { Id = default };

            await SavePointAsync(pointToSave);
        }

        foreach (var (key, importedPoint) in importedUnnumberedPoints)
        {
            var pointToSave = existingPointsByExternalId.TryGetValue(key, out var existingPoint)
                ? importedPoint with { Id = existingPoint.Id }
                : importedPoint with { Id = default };

            await SavePointAsync(pointToSave);
        }

        async Task SavePointAsync(StampingPoint pointToSave)
        {
            if (pointToSave.Id == default)
            {
                await _dbContext.AddAsync(pointToSave);
            }
            else
            {
                _dbContext.Update(pointToSave);
            }

            savedPoints.Add(pointToSave);
        }

        await _dbContext.SaveChangesAsync();
        savedPoints.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
        return savedPoints;
    }

    public async Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync(
        (Position Centre, decimal Range)? circularRange = null,
        int? userId = null,
        params StampingPoint[] stampingPoints)
    {
        var query = _dbContext.HikingTours.AsNoTracking();
        if (stampingPoints.Any())
        {
            var stampingPointIds = stampingPoints.Select(p => p.Id).Distinct().ToArray();
            query = query.Where(p => p.StampingPoints.Any(stampingPoint => stampingPointIds.Contains(stampingPoint.StampingPointId)));
        }

        var result = from tour in query
            join tourPoint in _dbContext.StampingPointsInTours.AsNoTracking() on tour.Id equals tourPoint.Tour.Id
            join point in _dbContext.StampingPoints.AsNoTracking()
                .Where(point => userId == null || _dbContext.UserStampingProviders.Any(access =>
                    access.UserId == userId.Value && access.StampingProviderId == point.ProviderId))
                on tourPoint.StampingPointId equals point.Id
            group point by tour into groupedStampingPoints
            select new { Tour = groupedStampingPoints.Key, Points = groupedStampingPoints.ToList() };

        var dto = await result.ToListAsync();
        if (circularRange != null)
        {
            dto = dto.Where(p => p.Points.Any(point => Position.GetDistance(point.Position, circularRange.Value.Centre) < circularRange.Value.Range)).ToList();
        }
        var providers = await GetStampingProvidersAsync(dto.SelectMany(p => p.Points).Select(p => p.ProviderId));
        var series = await GetStampingSeriesAsync(dto.SelectMany(p => p.Points).Select(p => p.SeriesId));
        return dto.Select(p =>
            (p.Tour, p.Points.Select(point => point with { Provider = providers[point.ProviderId], Series = series[point.SeriesId] }).ToList())).ToList();
    }

    public async Task SaveHikingToursAsync(params HikingTour[] tours)
    {
        List<HikingTour> updatedEntries = new();

        var updatedTours = tours.ToDictionary(p => p.Id);
        var allTours = await _dbContext.HikingTours.AsNoTracking().ToListAsync();

        foreach (var existingTour in allTours.Where(p => updatedTours.ContainsKey(p.Id)))
        {
            _dbContext.Update(updatedTours[existingTour.Id]);
            updatedEntries.Add(updatedTours[existingTour.Id]);
            updatedTours.Remove(existingTour.Id);
        }

        await _dbContext.AddRangeAsync(updatedTours.Values);
        updatedEntries.AddRange(updatedTours.Values);

        await _dbContext.SaveChangesAsync();
        updatedEntries.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
    }

    private async Task<Dictionary<int, StampingProvider>> GetStampingProvidersAsync(IEnumerable<int> providerIds)
    {
        var ids = providerIds.Distinct().ToArray();
        return await _dbContext.StampingProviders.AsNoTracking()
            .Where(provider => ids.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id);
    }

    private async Task<Dictionary<int, StampingSeries>> GetStampingSeriesAsync(IEnumerable<int> seriesIds)
    {
        var ids = seriesIds.Distinct().ToArray();
        return await _dbContext.StampingSeries.AsNoTracking()
            .Where(series => ids.Contains(series.Id))
            .ToDictionaryAsync(series => series.Id);
    }
}
