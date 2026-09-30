using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public sealed record SynchronizeVisitResult(
    StampingPoint StampingPoint,
    UserVisit? Visit,
    bool IsConflict);
