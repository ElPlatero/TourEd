namespace TourEd.Lib.Abstractions.Options;

public sealed class HarzerWandernadelConfiguration
{
    public long RelationId { get; set; }
    public Uri RelationApiUri { get; set; } = null!;
    public Uri RelationPublicUri { get; set; } = null!;
    public int MaxDownloadBytes { get; set; } = 5 * 1024 * 1024;
}
