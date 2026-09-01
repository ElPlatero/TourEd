using Api.Authentication;
using Api.Dto;
using Api.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Admin;

[ApiController, Route("api/admin/users"), Authorize(Policy = TouredAuthorizationPolicies.CliImport)]
public sealed class UsersController : ControllerBase
{
    private readonly AdminUserManager _manager;

    public UsersController(AdminUserManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public Task<List<AdminUserDto>> GetUsers(CancellationToken cancellationToken)
        => _manager.GetUsersAsync(cancellationToken);

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> DeleteUser(int userId, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUser().Id;
        if (userId == actorUserId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Self-deletion is not allowed",
                Detail = "The configured administrative CLI user cannot delete itself."
            });
        }

        return await _manager.DeleteUserAsync(userId, actorUserId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPut("{userId:int}/providers")]
    public async Task<ActionResult<AdminUserDto>> UpdateProviders(
        int userId,
        [FromBody] UpdateAdminUserProvidersRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Providers is null)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Providers is required." });
        }

        try
        {
            var updated = await _manager.UpdateProvidersAsync(
                userId,
                request,
                User.GetUser().Id,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Validation failed", Detail = exception.Message });
        }
    }
}
