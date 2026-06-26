namespace TourEd.Lib.Abstractions.Models;

public record StampingPoint(int Id, string Name, decimal Longitude, decimal Latitude, int Number, int Code, int ProviderId, string ExternalId)
{
    public Position Position { get; } = new(Longitude, Latitude);
    public StampingProvider Provider { get; init; } = null!;
}
