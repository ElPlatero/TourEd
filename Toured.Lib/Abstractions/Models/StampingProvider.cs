namespace TourEd.Lib.Abstractions.Models;

public class StampingProvider
{
    public const int TouringenId = 1;
    public const string TouringenSlug = "touringen";
    public const int HarzerWandernadelId = 2;
    public const string HarzerWandernadelSlug = "harzer-wandernadel";

    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Abbreviation { get; set; }
    public bool IsAnonymousAccessAllowed { get; set; }
    public Uri? WebsiteUri { get; set; }
    public string? Description { get; set; }
}
