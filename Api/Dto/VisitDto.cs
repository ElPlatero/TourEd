using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public record VisitDto(bool IsVisited, DateOnly? VisitedOn, TimeOnly? VisitedAt, StampingPointDto StampingPoint)
{
    public static VisitDto Create(UserVisit? visit, StampingPointDto stampingPoint) => new(
        visit is not null,
        visit?.Visited is { } visited ? DateOnly.FromDateTime(visited) : null,
        visit is { Visited: { } visitedWithTime, HasVisitedTime: true }
            ? TimeOnly.FromDateTime(visitedWithTime)
            : null,
        stampingPoint);
}
