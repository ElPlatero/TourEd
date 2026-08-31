using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Points;

[Authorize, ApiController, Route("api/[controller]")]
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
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _manager.GetStampingPointsAsync(query.Provider, currentUser.Id, query.GetGeoFilterOrDefault(), query.GetUserFilterOrDefault(currentUser));
            return Ok(new GetStampingPointsResponse(result.Count, result.OrderBy(p => p.Point.Provider.Slug).ThenBy(p => p.Point.Series.Slug).ThenBy(p => p.Point.Number.HasValue ? 0 : 1).ThenBy(p => p.Point.Number).ThenBy(p => p.Point.Name).Select(CreateDto)));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [Authorize]
    [HttpGet("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> GetVisit(int stampingPointNumber, [FromQuery] string? provider = null, [FromQuery] string? series = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            var (stampingPoint, userVisit) = await _manager.GetVisitAsync(currentUser, stampingPointNumber, provider, series);
            return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint, userVisit))));
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
    public async Task<IActionResult> AddVisit(int stampingPointNumber, [FromBody] SaveVisitRequest request, [FromQuery] string? provider = null, [FromQuery] string? series = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            await _manager.AddVisitAsync(currentUser, stampingPointNumber, request.VisitedOn, request.VisitedAt, provider, series);
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

    [Authorize]
    [HttpPatch("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> UpdateVisit(int stampingPointNumber, [FromBody] SaveVisitRequest request, [FromQuery] string? provider = null, [FromQuery] string? series = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            await _manager.UpdateVisitAsync(currentUser, stampingPointNumber, request.VisitedOn, request.VisitedAt, provider, series);
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
    }

    [Authorize]
    [HttpDelete("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> DeleteVisit(int stampingPointNumber, [FromQuery] string? provider = null, [FromQuery] string? series = null)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        try
        {
            await _manager.DeleteVisitAsync(currentUser, stampingPointNumber, provider, series);
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
    }

    [Authorize]
    [HttpGet("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> GetVisitById(int stampingPointId, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        try
        {
            var (stampingPoint, userVisit) = await _manager.GetVisitByIdAsync(currentUser, stampingPointId, provider);
            return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint, userVisit))));
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (NotSupportedException) { return BadRequest(); }
    }

    [Authorize]
    [HttpPut("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> AddVisitById(int stampingPointId, [FromBody] SaveVisitRequest request, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        try
        {
            await _manager.AddVisitByIdAsync(currentUser, stampingPointId, request.VisitedOn, request.VisitedAt, provider);
            return NoContent();
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (NotSupportedException) { return BadRequest(); }
        catch (InvalidOperationException) { return Conflict(); }
    }

    [Authorize]
    [HttpPatch("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> UpdateVisitById(int stampingPointId, [FromBody] SaveVisitRequest request, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        try
        {
            await _manager.UpdateVisitByIdAsync(currentUser, stampingPointId, request.VisitedOn, request.VisitedAt, provider);
            return NoContent();
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (NotSupportedException) { return BadRequest(); }
    }

    [Authorize]
    [HttpDelete("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> DeleteVisitById(int stampingPointId, [FromQuery] string? provider = null)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        try
        {
            await _manager.DeleteVisitByIdAsync(currentUser, stampingPointId, provider);
            return NoContent();
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (NotSupportedException) { return BadRequest(); }
    }
    
    private static StampingPointDto CreateDto((StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit) data)
    {
        var result = StampingPointDto.Create(data.Point, data.Visit);
        if (data.Tours != null) result.Tours = data.Tours.Select(TourCompactDto.Create);
        return result;
    }
}
