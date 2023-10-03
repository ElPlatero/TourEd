using System.Drawing;
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

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? visit)>> GetStampingPointsAsync(string? nameFilter = null, (Position Centre, decimal Radius)? geoFilter = null, int? userId = null, bool? excludeVisited = null, params int[] stampingPointsNr)
    {
        var query = _dbContext.StampingPoints.AsNoTracking();
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
        return dto.Select(p => (p.Point, p.Tours.Any(q => q != null) ? p.Tours : null, (UserVisit?) p.UserVisit)).ToList();
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
        return dto.Select(p => (p.Tour, p.Points)).ToList();
    }
    
    public async Task SaveStampingPointsAsync(params StampingPoint[] points)
    {
        List<StampingPoint> updatedEntries = new();
        var updatedPoints = points.ToDictionary(p => p.Id);
        var allPoints = await _dbContext.StampingPoints.AsNoTracking().ToListAsync();

        foreach (var existingPoint in allPoints.Where(p => updatedPoints.ContainsKey(p.Id)))
        {
            _dbContext.Update(updatedPoints[existingPoint.Id]);
            updatedEntries.Add(updatedPoints[existingPoint.Id]);
            updatedPoints.Remove(existingPoint.Id);
        }

        await _dbContext.AddRangeAsync(updatedPoints.Values);
        updatedEntries.AddRange(updatedPoints.Values);
        await _dbContext.SaveChangesAsync();
        updatedEntries.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
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
    
    
    public Task<User?> GetUserOrDefaultAsync(string userEmail) 
        => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(p => p.Email.ToLower() == userEmail.Trim().ToLower());

    public async Task<UserVisit?> GetUserVisitOrDefaultAsync(User currentUser, int stampingPointId) 
        => await _dbContext.UserVisits.FirstOrDefaultAsync(p => p.StampingPointId == stampingPointId && p.UserId == currentUser.Id);

    public async Task<StampingPoint> GetStampingPointAsync(int stampingPointNumber) 
        => await _dbContext.StampingPoints.FirstOrDefaultAsync(p => p.Number == stampingPointNumber) ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointNumber);

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
