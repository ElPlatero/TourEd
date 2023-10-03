using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Points;

public class StampingPointQuery
{
    [FromQuery(Name = "cen")]
    public string? Centre { get; set; }
    [FromQuery(Name = "rad")]
    public decimal Radius { get; set; }
    [FromQuery(Name = "vis")]
    public bool? ShowVisited { get; set; }

    public (Position, decimal)? GetGeoFilterOrDefault()
    {
        if (string.IsNullOrWhiteSpace(Centre)) return null;
        if (Radius <= 0) return null;

        var coordinatesplit = Centre.Split(',');
        if (coordinatesplit.Length != 2) return null;
        if (!decimal.TryParse(coordinatesplit[0], out var latitude)) return null;
        if (!decimal.TryParse(coordinatesplit[1], out var longitude)) return null;

        return (new Position(longitude, latitude), Radius * 1_000);
    }

    public (int UserId, bool ExcludeVisited)? GetUserFilterOrDefault(ClaimsPrincipal currentUser)
    {
        return ShowVisited != null
            ? (currentUser.GetUser().Id, !ShowVisited.Value)
            : null;
    }
}

