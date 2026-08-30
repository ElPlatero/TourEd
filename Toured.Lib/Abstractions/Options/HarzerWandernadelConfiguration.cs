namespace TourEd.Lib.Abstractions.Options;

public sealed class HarzerWandernadelConfiguration
{
    public Uri DownloadPageUri { get; set; } = null!;
    public Uri OverviewUri { get; set; } = null!;
    public int MaxDownloadBytes { get; set; } = 5 * 1024 * 1024;
}
