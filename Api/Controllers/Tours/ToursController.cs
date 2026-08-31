using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Tours;

[Authorize, ApiController, Route("api/[controller]")]
public class ToursController : ControllerBase
{
    private readonly TourDataManager _manager;

    public ToursController(TourDataManager manager)
    {
        _manager = manager;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetHikingTours([FromQuery] HikingTourQuery query)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        var result = await _manager.GetHikingToursAsync(
            currentUser.Id,
            query.Longitude != default && query.Latitude != default && query.Radius != default
                ? (new Position(query.Longitude, query.Latitude), query.Radius * 1000)
                : null);
        return Ok(new GetHikingToursResponse(result.Count, result.SelectMany(p => p.Points.Select(q => q.Id)).Distinct().Count(), result.Select(CreateDto)));
    }

    private static TourDto CreateDto((HikingTour Tour, List<StampingPoint>? Points) data)
    {
        var result = TourDto.Create(data.Tour);
        if (data.Points != null) result.StampingPoints = data.Points.Select(p => StampingPointDto.Create(p));
        return result;
    }

}
