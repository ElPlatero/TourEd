using Api.Repositories;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public class TourDataManager
{
    private readonly TouredRepository _repository;

    public TourDataManager(TouredRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit)>> GetStampingPointsAsync(string? providerSlug = null, int? currentUserId = null, (Position, decimal)? geoFilter = null, (int UserId, bool ExcludeVisited)? userFilter = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUserId);
        return await _repository.GetStampingPointsAsync(geoFilter: geoFilter, providerFilter: providerFilter, userId: userFilter?.UserId, excludeVisited: userFilter?.ExcludeVisited);
    }

    public async Task<(StampingPoint Point, List<HikingTour>? Tours)?> GetStampingPointOrDefaultAsync(int stampingPointId)
    {
        var points = await _repository.GetStampingPointsAsync(stampingPointsNr: stampingPointId);
        if (points.Count == 0)
        {
            return null;
        }

        var (point, tours, _) = points.First();
        return (point, tours);
    }

    public Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync((Position Centre, decimal Range)? distance = null)
    {
        return _repository.GetHikingToursAsync(distance);
    }

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitAsync(User currentUser, int stampingPointNumber, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter);
        var userVisit = await _repository.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task AddVisitAsync(User currentUser, int stampingPointNumber, DateTime? visited, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter);
        await _repository.AddUserVisitAsync(currentUser, stampingPoint.Id, visited);
    }
}
