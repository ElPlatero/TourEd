using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Points;

[ApiController, Route("api/[controller]")]
public class PointsController : ControllerBase
{
    private readonly TourDataManager _manager;

    public PointsController(TourDataManager manager)
    {
        _manager = manager;
    }
    
    [ProducesResponseType(typeof(GetStampingPointsResponse), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<IActionResult> GetStampingPoints([FromQuery] StampingPointQuery query)
    {
        var result = await _manager.GetStampingPointsAsync(query.GetGeoFilterOrDefault(), query.GetUserFilterOrDefault(User));
        return Ok(new GetStampingPointsResponse(result.Count, result.OrderBy(p => p.Point.Number).Select(CreateDto)));
    }

    [Authorize]
    [HttpGet("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> GetVisit(int stampingPointNumber)
    {
        try
        {
            var (stampingPoint, userVisit) = await _manager.GetVisitAsync(User.GetUser(), stampingPointNumber);
            return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint))));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpPut("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> AddVisit(int stampingPointNumber, [FromBody] AddVisitRequest request)
    {
        try
        {
            await _manager.AddVisitAsync(User.GetUser(), stampingPointNumber, request.Visited);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        } 
        catch (InvalidOperationException)
        {
            return Conflict();
        }
    }
    
    private static StampingPointDto CreateDto((StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit) data)
    {
        var result = StampingPointDto.Create(data.Point, data.Visit);
        if (data.Tours != null) result.Tours = data.Tours.Select(TourCompactDto.Create);
        return result;
    }
}