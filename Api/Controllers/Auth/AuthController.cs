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
    public IActionResult Login([FromQuery] string? returnUrl = null)
        => Challenge(
            new AuthenticationProperties
            {
                RedirectUri = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Content("~/")
            },
            TouredAuthenticationSchemes.GoogleChallenge);

    [HttpGet("session")]
    public async Task<ActionResult<AuthSessionResponse>> Session()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(TouredAuthenticationSchemes.Cookie);
        if (!authenticateResult.Succeeded ||
            authenticateResult.Principal?.Identity?.IsAuthenticated != true ||
            !authenticateResult.Principal.TryGetUser(out var user))
        {
            return Ok(new AuthSessionResponse(false, null, null));
        }

        var expiresAt = HttpContext.Items.TryGetValue(
            TouredAuthenticationSchemes.EffectiveCookieExpiresAtItem,
            out var effectiveExpiresAt)
            ? effectiveExpiresAt as DateTimeOffset?
            : authenticateResult.Properties?.ExpiresUtc;
        return Ok(new AuthSessionResponse(true, user.Email, expiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TouredAuthenticationSchemes.Cookie);
        return NoContent();
    }
}

public sealed record AuthSessionResponse(bool Authenticated, string? Email, DateTimeOffset? ExpiresAt);
