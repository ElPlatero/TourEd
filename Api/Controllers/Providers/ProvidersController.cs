using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Providers;

[Authorize, ApiController, Route("api/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly StampingProviderManager _manager;

    public ProvidersController(StampingProviderManager manager)
    {
        _manager = manager;
    }

    [ProducesResponseType(typeof(GetStampingProvidersResponse), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<GetStampingProvidersResponse>> GetStampingProviders()
    {
        if (!User.TryGetUser(out _))
        {
            return Unauthorized();
        }

        var providers = await _manager.GetStampingProvidersAsync(includeRestrictedProviders: true);
        return Ok(new GetStampingProvidersResponse(
            providers.Count,
            providers.Select(StampingProviderDetailsDto.Create)));
    }

    [Produces("application/geo+json")]
    [ProducesResponseType(typeof(StampingPointGeoJsonFeatureCollectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{providerSlug}/points.geojson")]
    public async Task<IActionResult> GetProviderData(
        string providerSlug,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUser(out _))
        {
            return Unauthorized();
        }
        var result = await _manager.GetPublicProviderDataAsync(providerSlug, cancellationToken);
        if (result is not { } providerData)
        {
            return NotFound();
        }

        return new JsonResult(StampingPointGeoJsonFeatureCollectionDto.Create(
            providerData.Provider,
            providerData.Points))
        {
            ContentType = "application/geo+json"
        };
    }
}
