using TourEd.Lib.Abstractions.Models;

namespace Api.Dto;

public sealed record StampingPointGeoJsonFeatureCollectionDto(
    string Type,
    string Name,
    string Attribution,
    string License,
    string LicenseUrl,
    string SourceUrl,
    string SourceRevision,
    DateTime SourceUpdatedAt,
    DateTime ImportedAt,
    IReadOnlyList<StampingPointGeoJsonFeatureDto> Features)
{
    public static StampingPointGeoJsonFeatureCollectionDto Create(
        StampingProvider provider,
        IReadOnlyList<StampingPoint> points)
        => new(
            "FeatureCollection",
            $"TourEd – {provider.Name}",
            provider.DataSourceAttribution!,
            provider.DataLicenseName!,
            provider.DataLicenseUri!.AbsoluteUri,
            provider.DataSourceUri!.AbsoluteUri,
            provider.DataSourceRevision!,
            DateTime.SpecifyKind(provider.DataSourceUpdatedAt!.Value, DateTimeKind.Utc),
            DateTime.SpecifyKind(provider.DataImportedAt!.Value, DateTimeKind.Utc),
            points.Select(point => StampingPointGeoJsonFeatureDto.Create(provider, point)).ToArray());
}

public sealed record StampingPointGeoJsonFeatureDto(
    string Type,
    string Id,
    StampingPointGeoJsonGeometryDto Geometry,
    StampingPointGeoJsonPropertiesDto Properties)
{
    public static StampingPointGeoJsonFeatureDto Create(StampingProvider provider, StampingPoint point)
        => new(
            "Feature",
            point.ExternalId,
            new StampingPointGeoJsonGeometryDto("Point", [point.Longitude, point.Latitude]),
            new StampingPointGeoJsonPropertiesDto(
                point.Number,
                point.Name,
                provider.Slug,
                $"{provider.Abbreviation ?? provider.Name} {point.Number:D3}"));
}

public sealed record StampingPointGeoJsonGeometryDto(string Type, decimal[] Coordinates);

public sealed record StampingPointGeoJsonPropertiesDto(
    int? Number,
    string Name,
    string Provider,
    string Reference);
