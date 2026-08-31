namespace TourEd.Lib.Abstractions.Options;

public class TouringenWebsiteConfiguration
{
    public long RelationId { get; set; } = 14773147;
    public Uri RelationApiUri { get; set; } = null!;
    public Uri RelationPublicUri { get; set; } = null!;
    public Uri StempelstellenUri { get; set; } = null!;
    public Uri NaturalTreasuresGpxUri { get; set; } = null!;
    public Uri RhoenFamilyTrailsGpxUri { get; set; } = null!;
    public int MaxDownloadBytes { get; set; } = 5 * 1024 * 1024;
}
