using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces.Services;

namespace Api.Authentication;

public sealed class CliBearerAuthenticationHandler : AuthenticationHandler<CliBearerAuthenticationOptions>
{
    private const string BearerScheme = "Bearer";
    private const string FailureMessage = "CLI authentication failed.";
    private readonly IUserService _userService;

    public CliBearerAuthenticationHandler(
        IUserService userService,
        IOptionsMonitor<CliBearerAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _userService = userService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationValue = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorizationValue))
        {
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationValue, out var authorization) ||
            !string.Equals(authorization.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter) ||
            string.IsNullOrWhiteSpace(Options.Token) ||
            string.IsNullOrWhiteSpace(Options.UserEmail) ||
            !CliTokenComparer.Matches(authorization.Parameter, Options.Token))
        {
            return AuthenticateResult.Fail(FailureMessage);
        }

        var user = await _userService.GetUserOrDefaultAsync(Options.UserEmail, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail(FailureMessage);
        }

        Claim[] claims =
        [
            new(Constants.ClaimsNames.UserId, user.Id.ToString()),
            new(Constants.ClaimsNames.UserEmail, user.Email)
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = BearerScheme;
        return Task.CompletedTask;
    }
}
