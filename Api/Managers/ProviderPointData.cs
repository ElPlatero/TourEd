using Api.Entities;

namespace Api.Managers;

/// <summary>A data-ready provider with its points, released for GeoJSON export.</summary>
public sealed record ProviderPointData(StampingProvider Provider, IReadOnlyList<StampingPoint> Points);
