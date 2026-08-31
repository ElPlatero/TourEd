namespace TourEd.Lib.Abstractions.Models;

public sealed record StampingPointSourceSnapshot(
    IReadOnlyList<StampingPoint> Points,
    Uri SourceUri,
    string Attribution,
    string LicenseName,
    Uri LicenseUri,
    string Revision,
    DateTime SourceUpdatedAt);
