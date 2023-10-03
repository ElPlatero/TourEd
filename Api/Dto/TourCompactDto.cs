using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record TourCompactDto(int Id, string Name, bool IsKidsTour, bool IsCircularPath, bool IsLongDistanceTrail)
{
    public static TourCompactDto Create(HikingTour tour) => new(tour.Id, tour.Name, tour.IsKidsTour, tour.IsCircularPath, tour.IsLongDistanceTrail);
}