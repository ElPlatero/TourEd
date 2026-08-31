namespace TourEd.Lib.Abstractions.Options;

public class TouringenWebsiteConfiguration
{
    public Uri StempelstellenUri { get; set; } = null!;
    public Uri StandardGpxUri { get; set; } = null!;
    public Uri NaturalTreasuresGpxUri { get; set; } = null!;
    public Uri RhoenFamilyTrailsGpxUri { get; set; } = null!;
    public int MaxDownloadBytes { get; set; } = 5 * 1024 * 1024;
}
