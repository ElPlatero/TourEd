using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public readonly record struct VisitStateValue(bool IsVisited, DateTime? Visited, bool HasVisitedTime)
{
    public static VisitStateValue Open => new(false, null, false);

    public static VisitStateValue FromVisit(UserVisit? visit) => visit is null
        ? Open
        : new VisitStateValue(true, visit.Visited, visit.HasVisitedTime);
}

public sealed record SynchronizeVisitResult(
    StampingPoint StampingPoint,
    UserVisit? Visit,
    bool IsConflict);
