using Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Models;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/[controller]"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public class ImportsController : ControllerBase
{
    private readonly IImportManager _importManager;

    public ImportsController(IImportManager importManager)
    {
        _importManager = importManager;
    }
    
    [HttpPost("touringen")]
    public async Task<IActionResult> CreateNewTouringenImport(CancellationToken cancellationToken)
    {
        await _importManager.ImportTouringenDataAsync(cancellationToken);
        return Ok();
    }

    [HttpPost("harzer-wandernadel")]
    public async Task<IActionResult> CreateNewHarzerWandernadelImport(CancellationToken cancellationToken)
    {
        await _importManager.ImportHarzerWandernadelDataAsync(cancellationToken);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> CreateNewUserDataImport([FromForm] IFormFileCollection csvImport)
    {
        if (csvImport.Count != 1 || csvImport[0].Length == 0)
        {
            return BadRequest(new UserDataImportResult(0, 0, 0,
                [new(null, "Upload exactly one non-empty CSV file in csvImport.")]));
        }
        await using var stream = csvImport[0].OpenReadStream();
        try
        {
            var result = await _importManager.ImportUserDataAsync(stream);
            return result.Errors.Count > 0 ? BadRequest(result) : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
    }
}
