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
        var providers = await _manager.GetStampingProvidersAsync();
        return Ok(new GetStampingProvidersResponse(
            providers.Count,
            providers.Select(StampingProviderDetailsDto.Create)));
    }
}
