using System.Collections;
using System.Text.Json.Serialization;
using TourEd.Lib.Json;

namespace TourEd.Lib.Abstractions.Models;

public record RawArea(
    [property: JsonPropertyName("uid")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("touren")] RawTour[] Touren,
    RawStampPoint[]? OrphanedStampPoints)
{
    [JsonPropertyName("stamp_points_without_tour"), JsonConverter(typeof(FalseOrNullConverter<RawStampPoint[]>))]
    public RawStampPoint[] OrphanedStampPoints { get; } = OrphanedStampPoints ?? Array.Empty<RawStampPoint>();
}
