using Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Abstractions.Interfaces;

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
        await using var stream = csvImport[0].OpenReadStream();
        try
        {
            await _importManager.ImportUserDataAsync(stream);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        return Ok();
    }
}
