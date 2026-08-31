using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions;

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
        [FromServices] IUnitOfWork unitOfWork,
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

        try
        {
            using (unitOfWork)
            {
                var result = await _tourDataManager.SaveAdminStampingPointsAsync(requests, cancellationToken);
                await unitOfWork.CommitAsync();
                return Ok(result);
            }
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation failed",
                Detail = ex.Message
            });
        }
    }
}
