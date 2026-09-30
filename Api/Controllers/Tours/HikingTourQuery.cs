using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Tours;

public sealed class HikingTourQuery
{
    [FromQuery(Name = "lat")]
    public decimal Latitude { get; set; }
    [FromQuery(Name = "lon")]
    public decimal Longitude { get; set; }
    [FromQuery(Name = "rad")]
    public decimal Radius { get; set; }
}
