namespace Api.Entities;

/// <summary>A circular area around <paramref name="Centre"/> with a radius in metres.</summary>
public sealed record GeoCircle(Position Centre, decimal Radius)
{
    public bool Contains(Position position) => Position.GetDistance(position, Centre) < Radius;
}
