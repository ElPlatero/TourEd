using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/registrations"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public sealed class RegistrationsController : ControllerBase
{
    private readonly AdminUserManager _manager;

    public RegistrationsController(AdminUserManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public Task<List<AdminRegistrationRequestDto>> GetRegistrations(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => _manager.GetRegistrationRequestsAsync(status, cancellationToken);

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<AdminRegistrationRequestDto>> ApproveRegistration(
        int id,
        CancellationToken cancellationToken)
    {
        var updated = await _manager.ApproveRegistrationRequestAsync(
            id,
            User.GetUser().Id,
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<AdminRegistrationRequestDto>> RejectRegistration(
        int id,
        CancellationToken cancellationToken)
    {
        var updated = await _manager.RejectRegistrationRequestAsync(
            id,
            User.GetUser().Id,
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }
}
