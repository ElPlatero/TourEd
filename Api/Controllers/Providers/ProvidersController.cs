using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Providers;

[ApiController, AllowAnonymous, Route("api/providers")]
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
        var providers = await _manager.GetStampingProvidersAsync(User.Identity?.IsAuthenticated == true);
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
