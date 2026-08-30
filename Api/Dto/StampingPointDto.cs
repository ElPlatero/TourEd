using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record StampingPointDto(
    int Number,
    string Name,
    Position Position,
    bool IsVisited,
    DateOnly? VisitedOn,
    TimeOnly? VisitedAt,
    StampingProviderDto Provider)
{
    public IEnumerable<TourCompactDto>? Tours { get; set; }
    public static StampingPointDto Create(StampingPoint point, UserVisit? visit = null) => new(
        point.Number,
        point.Name,
        point.Position,
        visit is not null,
        visit?.Visited is { } visited ? DateOnly.FromDateTime(visited) : null,
        visit is { Visited: { } visitedWithTime, HasVisitedTime: true }
            ? TimeOnly.FromDateTime(visitedWithTime)
            : null,
        StampingProviderDto.Create(point.Provider));
}
