using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record StampingPointDto(int Number, string Name, Position Position, DateTime? Visited)
{
    public IEnumerable<TourCompactDto>? Tours { get; set; }
    public static StampingPointDto Create(StampingPoint point, UserVisit? visit = null) => new(point.Number, point.Name, point.Position, visit?.Visited);
}