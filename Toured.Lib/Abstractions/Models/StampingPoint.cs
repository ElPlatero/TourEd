namespace TourEd.Lib.Abstractions.Models;

public record StampingPoint(int Id, string Name, decimal Longitude, decimal Latitude, int Number, int Code)
{
    public Position Position { get; } = new(Longitude, Latitude);
}