using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/providers"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public sealed class ProvidersController : ControllerBase
{
    private readonly AdminUserManager _manager;

    public ProvidersController(AdminUserManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public Task<List<AdminProviderDto>> GetProviders(CancellationToken cancellationToken)
        => _manager.GetProvidersAsync(cancellationToken);
}
