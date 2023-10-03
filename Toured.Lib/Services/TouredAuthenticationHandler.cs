using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Services;

public class TouredAuthenticationHandler : AuthenticationHandler<EmailHeaderAuthenticationOptions>
{
    private readonly IUserService _userService;

    public TouredAuthenticationHandler(IUserService userService, IOptionsMonitor<EmailHeaderAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
    {
        _userService = userService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(EmailHeaderAuthenticationOptions.HeaderName))
        {
            return AuthenticateResult.Fail($"""Toured Authentication Header "{EmailHeaderAuthenticationOptions.HeaderName}" missing.""");
        }

        string userEmail = Request.Headers[EmailHeaderAuthenticationOptions.HeaderName]!;

        var user = await _userService.GetUserOrDefaultAsync(userEmail);
        if(user == null)
        {
            return AuthenticateResult.Fail($"Unknown user: {userEmail}.");
        }
        List<Claim> claims = new ()
        {
            new Claim(Constants.ClaimsNames.UserId, user.Id.ToString()),
            new Claim(Constants.ClaimsNames.UserEmail, user.Email)
        };

        ClaimsPrincipal claimsPrincipal = new (new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name));
    }
}
