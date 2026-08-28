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
        User? currentUser = null;
        if (User.Identity?.IsAuthenticated == true && !User.TryGetUser(out currentUser))
        {
            return Unauthorized();
        }

        if (query.ShowVisited != null && currentUser is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _manager.GetStampingPointsAsync(query.Provider, currentUser?.Id, query.GetGeoFilterOrDefault(), query.GetUserFilterOrDefault(currentUser));
            return Ok(new GetStampingPointsResponse(result.Count, result.OrderBy(p => p.Point.Provider.Slug).ThenBy(p => p.Point.Number).Select(CreateDto)));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpGet("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> GetVisit(int stampingPointNumber, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            var (stampingPoint, userVisit) = await _manager.GetVisitAsync(currentUser, stampingPointNumber, provider);
            return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint))));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return BadRequest();
        }
    }

    [Authorize]
    [HttpPut("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> AddVisit(int stampingPointNumber, [FromBody] AddVisitRequest request, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            await _manager.AddVisitAsync(currentUser, stampingPointNumber, request.Visited, provider);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        } 
        catch (NotSupportedException)
        {
            return BadRequest();
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
