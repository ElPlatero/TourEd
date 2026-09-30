using Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Points;

public sealed class StampingPointQuery
{
    [FromQuery(Name = "cen")]
    public string? Centre { get; set; }
    [FromQuery(Name = "rad")]
    public decimal Radius { get; set; }
    [FromQuery(Name = "vis")]
    public bool? ShowVisited { get; set; }
    [FromQuery(Name = "provider")]
    public string? Provider { get; set; }

    public GeoCircle? GetAreaOrDefault()
    {
        if (string.IsNullOrWhiteSpace(Centre)) return null;
        if (Radius <= 0) return null;

        var coordinatesplit = Centre.Split(',');
        if (coordinatesplit.Length != 2) return null;
        if (!decimal.TryParse(coordinatesplit[0], out var latitude)) return null;
        if (!decimal.TryParse(coordinatesplit[1], out var longitude)) return null;

        return new GeoCircle(new Position(longitude, latitude), Radius * 1_000);
    }

    /// <summary><c>vis=true</c> excludes open points, <c>vis=false</c> excludes visited points.</summary>
    public bool? GetExcludeVisitedOrDefault() => ShowVisited is { } showVisited ? !showVisited : null;
}
