using System.Text.RegularExpressions;
using System.Xml.Linq;
using Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/[controller]"), Authorize]
public class ImportsController : ControllerBase
{
    private readonly IImportManager _importManager;

    public ImportsController(IImportManager importManager)
    {
        _importManager = importManager;
    }
    
    [HttpPost("touringen")]
    public async Task<IActionResult> CreateNewTouringenImport([FromServices] IUnitOfWork unitOfWork)
    {
        using (unitOfWork)
        {
            await _importManager.ImportTouringenDataAsync();
            await unitOfWork.CommitAsync();
            return Ok();
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateNewUserDataImport([FromForm] IFormFileCollection csvImport,[FromServices] IUnitOfWork unitOfWork)
    {
        using (unitOfWork)
        await using (var stream = csvImport[0].OpenReadStream())
        {
            await _importManager.ImportUserDataAsync(stream);
            await unitOfWork.CommitAsync();
        }

        return Ok();
    }

}
