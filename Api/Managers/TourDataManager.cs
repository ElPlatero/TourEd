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

    public Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit)>> GetStampingPointsAsync((Position, decimal)? geoFilter = null, (int UserId, bool ExcludeVisited)? userFilter = null)
    {
         return _repository.GetStampingPointsAsync(geoFilter: geoFilter, userId: userFilter?.UserId, excludeVisited: userFilter?.ExcludeVisited);
    }

    public async Task<(StampingPoint Point, List<HikingTour>? Tours)?> GetStampingPointOrDefaultAsync(int stampingPointId)
    {
        var points = await _repository.GetStampingPointsAsync(null, null, stampingPointId);
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

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitAsync(User currentUser, int stampingPointNumber)
    {
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber);
        var userVisit = await _repository.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task AddVisitAsync(User currentUser, int stampingPointNumber, DateTime? visited)
    {
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber);
        await _repository.AddUserVisitAsync(currentUser, stampingPoint.Id, visited);
    }
}
