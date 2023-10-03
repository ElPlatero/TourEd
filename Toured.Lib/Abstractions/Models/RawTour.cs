using System.Text.Json.Serialization;
using TourEd.Lib.Json;

namespace TourEd.Lib.Abstractions.Models;

public record RawTour(
    [property: JsonPropertyName("uid")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("stamp_points")] RawStampPoint[] StampPoints,
    [property: JsonPropertyName("kidstour"), JsonConverter(typeof(BooleanConverter))] bool IsKidsTour,
    [property: JsonPropertyName("rundweg"), JsonConverter(typeof(BooleanConverter))] bool IsCircularPath,
    [property: JsonPropertyName("fernwanderweg"), JsonConverter(typeof(BooleanConverter))] bool IsLongDistanceTrail,
    [property: JsonPropertyName("komoot_link")] string? KomootLink,
    [property: JsonPropertyName("start_point_desc")] string? StartPointDescription,
    [property: JsonPropertyName("end_point_desc")] string? EndPointDescription
);
