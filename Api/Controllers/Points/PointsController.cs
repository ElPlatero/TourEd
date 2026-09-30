using Api.Authentication;
using Api.Dto;
using Api.Entities;
using Api.Managers;
using Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Points;

[Authorize, ApiController, Route("api/[controller]")]
public sealed class PointsController : ControllerBase
{
    private readonly TourDataManager _manager;

    public PointsController(TourDataManager manager)
    {
        _manager = manager;
    }

    [ProducesResponseType(typeof(GetStampingPointsResponse), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<IActionResult> GetStampingPoints([FromQuery] StampingPointQuery query, CancellationToken cancellationToken)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        var result = await _manager.GetStampingPointsAsync(
            query.Provider,
            currentUser.Id,
            query.GetAreaOrDefault(),
            query.GetExcludeVisitedOrDefault(),
            cancellationToken);
        var points = result
            .OrderBy(p => p.Point.Provider.Slug)
            .ThenBy(p => p.Point.Series.Slug)
            .ThenBy(p => p.Point.Number.HasValue ? 0 : 1)
            .ThenBy(p => p.Point.Number)
            .ThenBy(p => p.Point.Name)
            .Select(CreateDto);
        return Ok(new GetStampingPointsResponse(result.Count, points));
    }

    [Authorize]
    [HttpGet("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> GetVisit(
        int stampingPointNumber,
        [FromQuery] string? provider = null,
        [FromQuery] string? series = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        var (stampingPoint, userVisit) = await _manager.GetVisitAsync(currentUser, stampingPointNumber, provider, series, cancellationToken);
        return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint, userVisit))));
    }

    [Authorize]
    [HttpPut("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> AddVisit(
        int stampingPointNumber,
        [FromBody] SaveVisitRequest request,
        [FromQuery] string? provider = null,
        [FromQuery] string? series = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        await _manager.AddVisitAsync(currentUser, stampingPointNumber, request.VisitedOn, request.VisitedAt, provider, series, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> UpdateVisit(
        int stampingPointNumber,
        [FromBody] SaveVisitRequest request,
        [FromQuery] string? provider = null,
        [FromQuery] string? series = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        await _manager.UpdateVisitAsync(currentUser, stampingPointNumber, request.VisitedOn, request.VisitedAt, provider, series, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{stampingPointNumber:int:min(1)}")]
    public async Task<IActionResult> DeleteVisit(
        int stampingPointNumber,
        [FromQuery] string? provider = null,
        [FromQuery] string? series = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser))
        {
            return Unauthorized();
        }

        await _manager.DeleteVisitAsync(currentUser, stampingPointNumber, provider, series, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("id/{stampingPointId:int:min(1)}/state")]
    [ProducesResponseType(typeof(VisitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(VisitDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SynchronizeVisitById(
        int stampingPointId,
        [FromBody] SynchronizeVisitRequest request,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        var result = await _manager.SynchronizeVisitByIdAsync(
            currentUser,
            stampingPointId,
            CreateState(request.Expected!),
            CreateState(request.Desired!),
            provider,
            cancellationToken);
        var dto = VisitDto.Create(result.Visit, StampingPointDto.Create(result.StampingPoint, result.Visit));
        return result.IsConflict ? Conflict(dto) : Ok(dto);
    }

    [Authorize]
    [HttpGet("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> GetVisitById(int stampingPointId, [FromQuery] string? provider = null, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        var (stampingPoint, userVisit) = await _manager.GetVisitByIdAsync(currentUser, stampingPointId, provider, cancellationToken);
        return Ok(new GetVisitResult(VisitDto.Create(userVisit, StampingPointDto.Create(stampingPoint, userVisit))));
    }

    [Authorize]
    [HttpPut("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> AddVisitById(
        int stampingPointId,
        [FromBody] SaveVisitRequest request,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        await _manager.AddVisitByIdAsync(currentUser, stampingPointId, request.VisitedOn, request.VisitedAt, provider, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> UpdateVisitById(
        int stampingPointId,
        [FromBody] SaveVisitRequest request,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        await _manager.UpdateVisitByIdAsync(currentUser, stampingPointId, request.VisitedOn, request.VisitedAt, provider, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("id/{stampingPointId:int:min(1)}")]
    public async Task<IActionResult> DeleteVisitById(int stampingPointId, [FromQuery] string? provider = null, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUser(out var currentUser)) return Unauthorized();
        await _manager.DeleteVisitByIdAsync(currentUser, stampingPointId, provider, cancellationToken);
        return NoContent();
    }

    private static StampingPointDto CreateDto(StampingPointDetails data)
    {
        var result = StampingPointDto.Create(data.Point, data.Visit);
        if (data.Tours != null) result.Tours = data.Tours.Select(TourCompactDto.Create);
        return result;
    }

    private static VisitStateValue CreateState(VisitStateRequest state) => state.IsVisited
        ? new VisitStateValue(
            true,
            state.VisitedOn?.ToDateTime(state.VisitedAt ?? TimeOnly.MinValue),
            state.VisitedAt.HasValue)
        : VisitStateValue.Open;
}
