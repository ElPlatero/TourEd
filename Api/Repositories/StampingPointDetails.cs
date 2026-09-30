using Api.Entities;

namespace Api.Repositories;

/// <summary>A stamping point with its provider and series loaded, its hiking tours and the requesting user's visit.</summary>
public sealed record StampingPointDetails(StampingPoint Point, IReadOnlyList<HikingTour>? Tours, UserVisit? Visit);
