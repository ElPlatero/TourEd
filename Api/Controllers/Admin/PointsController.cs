using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/[controller]"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public class PointsController : ControllerBase
{
    private readonly TourDataManager _tourDataManager;

    public PointsController(TourDataManager tourDataManager)
    {
        _tourDataManager = tourDataManager;
    }

    [HttpPost, HttpPut]
    public async Task<IActionResult> SavePoints(
        [FromBody] IReadOnlyList<AdminStampingPointRequestDto> requests,
        [FromServices] IUnitOfWorkFactory unitOfWorkFactory,
        CancellationToken cancellationToken)
    {
        if (requests == null || requests.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "At least one stamping point must be provided."
            });
        }

        await using var unitOfWork = await unitOfWorkFactory.BeginAsync(cancellationToken);
        var result = await _tourDataManager.SaveAdminStampingPointsAsync(requests, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(result);
    }
}
