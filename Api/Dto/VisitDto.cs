using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record VisitDto(DateTime? Visited, StampingPointDto StampingPoint)
{
    public bool IsVisited => Visited.HasValue;
    public static VisitDto Create(UserVisit? visit, StampingPointDto stampingPoint) => new(visit?.Visited, stampingPoint);
}
