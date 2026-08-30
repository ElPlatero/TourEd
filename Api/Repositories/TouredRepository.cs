using System.Drawing;
using Api.Managers;
using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public class TouredRepository : IUserService
{
    private readonly DataContext _dbContext;

    public TouredRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StampingProviderFilter> GetStampingProviderFilterAsync(string? providerSlug = null, int? userId = null)
    {
        if (string.Equals(providerSlug, "all", StringComparison.OrdinalIgnoreCase))
        {
            return StampingProviderFilter.All;
        }

        if (!string.IsNullOrWhiteSpace(providerSlug))
        {
            var provider = await _dbContext.StampingProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Slug.ToLower() == providerSlug.Trim().ToLowerInvariant());
            return provider == null
                ? throw EntityNotFoundException.Create<StampingProvider>(providerSlug)
                : StampingProviderFilter.Single(provider.Id);
        }

        if (userId != null)
        {
            var defaultProviderId = await _dbContext.Users.AsNoTracking()
                .Where(p => p.Id == userId.Value)
                .Select(p => p.DefaultStampingProviderId)
                .FirstOrDefaultAsync();
            if (defaultProviderId != default)
            {
                return StampingProviderFilter.Single(defaultProviderId);
            }
        }

        return StampingProviderFilter.Single(StampingProvider.TouringenId);
    }

    public Task<List<StampingProvider>> GetStampingProvidersAsync()
        => _dbContext.StampingProviders.AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Slug)
            .ToListAsync();

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? visit)>> GetStampingPointsAsync(string? nameFilter = null, (Position Centre, decimal Radius)? geoFilter = null, StampingProviderFilter? providerFilter = null, int? userId = null, bool? excludeVisited = null, params int[] stampingPointsNr)
    {
        IQueryable<StampingPoint> query = _dbContext.StampingPoints.AsNoTracking();
        if (providerFilter is { IncludesAllProviders: false, ProviderId: { } providerId })
        {
            query = query.Where(p => p.ProviderId == providerId);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.Trim().ToLowerInvariant()));
        }

        if (stampingPointsNr.Length > 0)
        {
            query = query.Where(p => stampingPointsNr.Contains(p.Number));
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
        return dto.Select(p =>
            (p.Point with { Provider = providers[p.Point.ProviderId] },
                p.Tours.Any(q => q != null) ? p.Tours : null,
                (UserVisit?) p.UserVisit)).ToList();
    }

    public async Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync((Position Centre, decimal Range)? circularRange = null, params StampingPoint[] stampingPoints)
    {
        var query = _dbContext.HikingTours.AsNoTracking();
        if (stampingPoints.Any())
        {
            var stampingPointIds = stampingPoints.Select(p => p.Id).Distinct().ToArray();
            query = query.Where(p => p.StampingPoints.Any(stampingPoint => stampingPointIds.Contains(stampingPoint.StampingPointId)));
        }

        var result = from tour in query
            join tourPoint in _dbContext.StampingPointsInTours.AsNoTracking() on tour.Id equals tourPoint.Tour.Id
            join point in _dbContext.StampingPoints.AsNoTracking() on tourPoint.StampingPointId equals point.Id
            group point by tour into groupedStampingPoints
            select new { Tour = groupedStampingPoints.Key, Points = groupedStampingPoints.ToList() };

        var dto = await result.ToListAsync();
        if (circularRange != null)
        {
            dto = dto.Where(p => p.Points.Any(point => Position.GetDistance(point.Position, circularRange.Value.Centre) < circularRange.Value.Range)).ToList();
        }
        var providers = await GetStampingProvidersAsync(dto.SelectMany(p => p.Points).Select(p => p.ProviderId));
        return dto.Select(p =>
            (p.Tour, p.Points.Select(point => point with { Provider = providers[point.ProviderId] }).ToList())).ToList();
    }

    private async Task<Dictionary<int, StampingProvider>> GetStampingProvidersAsync(IEnumerable<int> providerIds)
    {
        var ids = providerIds.Distinct().ToArray();
        return await _dbContext.StampingProviders.AsNoTracking()
            .Where(provider => ids.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id);
    }
    
    public async Task<IReadOnlyList<StampingPoint>> SaveStampingPointsAsync(params StampingPoint[] points)
    {
        if (points.Length == 0)
        {
            return Array.Empty<StampingPoint>();
        }

        var importedPoints = points.ToDictionary(p => (p.ProviderId, p.Number));
        var providerIds = importedPoints.Keys.Select(p => p.ProviderId).Distinct().ToArray();
        var existingPoints = await _dbContext.StampingPoints.AsNoTracking()
            .Where(p => providerIds.Contains(p.ProviderId))
            .ToDictionaryAsync(p => new { p.ProviderId, p.Number });
        var savedPoints = new List<StampingPoint>(importedPoints.Count);

        foreach (var (key, importedPoint) in importedPoints)
        {
            var pointToSave = existingPoints.TryGetValue(new { key.ProviderId, key.Number }, out var existingPoint)
                ? importedPoint with { Id = existingPoint.Id }
                : importedPoint with { Id = default };

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

    public async Task SaveImportAsync(int stampingPointsCount, int hikingToursCount)
    {
        await _dbContext.AddAsync(new Import(default, default, stampingPointsCount, hikingToursCount));
        await _dbContext.SaveChangesAsync();
    }

    public async Task SaveUserDataAsync(params UserVisit[] visits)
    {
        if (!visits.Any()) return;
        if (visits.Select(p => p.UserId).Distinct().Count() > 1) throw new InvalidOperationException("Can only import one user at a time.");
        if (visits.GroupBy(p => p.StampingPointId).Any(p => p.Count() > 1)) throw new InvalidOperationException("Stamping points can only be visited once. Remove duplicate entries.");
        var updatedVisits = visits.ToDictionary(p => p.StampingPointId);
        List<UserVisit> updatedEntries = new();
        var allVisits = await _dbContext.UserVisits.AsNoTracking().Where(p => p.UserId == visits.First().UserId).ToListAsync();

        foreach (var existingEntry in allVisits.Where(p => updatedVisits.ContainsKey(p.StampingPointId)))
        {
            updatedVisits.Remove(existingEntry.StampingPointId);
        }

        await _dbContext.AddRangeAsync(updatedVisits.Values);
        updatedEntries.AddRange(updatedVisits.Values);

        await _dbContext.SaveChangesAsync();
        updatedEntries.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
    }
    
    
    public Task<User?> GetUserOrDefaultAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = userEmail.Trim().ToLowerInvariant();
        return _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<User?> GetUserByGoogleSubjectOrDefaultAsync(string googleSubject, CancellationToken cancellationToken = default)
        => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(p => p.GoogleSubject == googleSubject, cancellationToken);

    public async Task<bool> TryBindGoogleSubjectAsync(int userId, string googleSubject, CancellationToken cancellationToken = default)
    {
        var updatedUsers = await _dbContext.Users
            .Where(user => user.Id == userId &&
                           user.GoogleSubject == null &&
                           !_dbContext.Users.Any(other => other.GoogleSubject == googleSubject))
            .ExecuteUpdateAsync(
                properties => properties.SetProperty(user => user.GoogleSubject, googleSubject),
                cancellationToken);

        return updatedUsers == 1;
    }

    public async Task<UserVisit?> GetUserVisitOrDefaultAsync(User currentUser, int stampingPointId) 
        => await _dbContext.UserVisits.FirstOrDefaultAsync(p => p.StampingPointId == stampingPointId && p.UserId == currentUser.Id);

    public async Task<StampingPoint> GetStampingPointAsync(int stampingPointNumber, StampingProviderFilter providerFilter)
    {
        if (providerFilter.IncludesAllProviders)
        {
            throw new NotSupportedException("A single stamping point lookup requires one provider.");
        }

        return await _dbContext.StampingPoints.Include(p => p.Provider).FirstOrDefaultAsync(p => p.Number == stampingPointNumber && p.ProviderId == providerFilter.ProviderId)
               ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointNumber);
    }

    public async Task AddUserVisitAsync(User currentUser, int stampingPointId, DateTime? visited)
    {
        var dto = await _dbContext.UserVisits.SingleOrDefaultAsync(p => p.UserId == currentUser.Id && p.StampingPointId == stampingPointId);
        if (dto == null)
        {
            dto = new UserVisit
            {
                StampingPointId = stampingPointId,
                UserId = currentUser.Id,
                Visited = visited
            };
            await _dbContext.AddAsync(dto);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            throw new InvalidOperationException("This stamping point has already been visited.");
        }
    }
}
