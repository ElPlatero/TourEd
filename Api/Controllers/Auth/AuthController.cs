using Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourEd.Lib.Extensions;

namespace Api.Controllers.Auth;

[ApiController, AllowAnonymous, Route("auth")]
public sealed class AuthController : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login()
        => Challenge(
            new AuthenticationProperties { RedirectUri = Url.Content("~/") },
            TouredAuthenticationSchemes.GoogleChallenge);

    [HttpGet("session")]
    public ActionResult<AuthSessionResponse> Session()
    {
        if (User.Identity?.IsAuthenticated != true || !User.TryGetUser(out var user))
        {
            return Ok(new AuthSessionResponse(false, null));
        }

        return Ok(new AuthSessionResponse(true, user.Email));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TouredAuthenticationSchemes.Cookie);
        return NoContent();
    }
}

public sealed record AuthSessionResponse(bool Authenticated, string? Email);
