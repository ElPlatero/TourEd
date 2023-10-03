using System.Text.Json.Serialization;

namespace TourEd.Lib.Abstractions.Models;

public record RawStampPoint(
    [property: JsonPropertyName("uid")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("latitude")] decimal Latitude,
    [property: JsonPropertyName("longitude")] decimal Longitude,
    [property: JsonPropertyName("positionsnummer")] int Positionsnummer,
    [property: JsonPropertyName("stempelkastennr3")] int StampPointNumber,
    [property: JsonPropertyName("stempelkastencode6")] int StampPointExtendedNumber,
    [property: JsonPropertyName("stempelstellenname")] string? Name
);
