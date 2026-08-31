namespace TourEd.Lib.Abstractions.Models;

public record StampingPoint(int Id, string Name, decimal Longitude, decimal Latitude, int? Number, int Code, int ProviderId, string ExternalId)
{
    public int SeriesId { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public Position Position { get; } = new(Longitude, Latitude);
    public StampingProvider Provider { get; init; } = null!;
    public StampingSeries Series { get; init; } = null!;
}
