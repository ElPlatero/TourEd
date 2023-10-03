namespace TourEd.Lib.Abstractions.Models;

public record HikingTour(int Id, string Name, string? Startpoint, string? Endpoint, Uri? KomootUri, bool IsKidsTour, bool IsCircularPath, bool IsLongDistanceTrail)
{
    public List<SortedStampingPoint> StampingPoints { get; set; } = null!;
}
