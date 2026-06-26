namespace TourEd.Lib.Abstractions.Models;

public class StampingProvider
{
    public const int TouringenId = 1;
    public const string TouringenSlug = "touringen";

    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Uri? WebsiteUri { get; set; }
    public string? Description { get; set; }
}
