using Api.Entities;

namespace Api.Repositories;

/// <summary>A hiking tour with the stamping points on it that the user may see.</summary>
public sealed record HikingTourWithPoints(HikingTour Tour, IReadOnlyList<StampingPoint> Points);
