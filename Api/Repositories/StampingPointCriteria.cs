using Api.Entities;

namespace Api.Repositories;

/// <summary>Filters for <see cref="StampingPointRepository.GetStampingPointsAsync"/>; unset values do not filter.</summary>
public sealed record StampingPointCriteria
{
    public StampingProviderFilter? ProviderFilter { get; init; }
    public string? SeriesSlug { get; init; }
    public string? NameFilter { get; init; }
    public GeoCircle? Area { get; init; }
    public IReadOnlyCollection<int>? StampingPointNumbers { get; init; }

    /// <summary>Loads this user's visit for each point.</summary>
    public int? UserId { get; init; }

    /// <summary><c>true</c> returns only unvisited, <c>false</c> only visited points of <see cref="UserId"/>.</summary>
    public bool? ExcludeVisited { get; init; }
}
