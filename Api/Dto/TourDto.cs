using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record TourDto(int Id, string Name, bool IsKidsTour, bool IsCircularPath, bool IsLongDistanceTrail, string? Startpoint, string? Endpoint, Uri? KomootUri) : TourCompactDto(Id, Name, IsKidsTour, IsCircularPath, IsLongDistanceTrail)
{
    public IEnumerable<StampingPointDto>? StampingPoints { get; set; } 
    public new static TourDto Create(HikingTour tour) => new(tour.Id, tour.Name, tour.IsKidsTour, tour.IsCircularPath, tour.IsLongDistanceTrail, tour.Startpoint, tour.Endpoint, tour.KomootUri);
}