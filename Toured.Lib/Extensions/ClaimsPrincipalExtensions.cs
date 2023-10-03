using System.Security.Authentication;
using System.Security.Claims;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static User GetUser(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst(Constants.ClaimsNames.UserId)?.Value ?? throw new AuthenticationException("Missing claim.");
        var userEmail = principal.FindFirst(Constants.ClaimsNames.UserEmail)?.Value ?? throw new AuthenticationException("Missing claim.");
        return new User
        {
            Id = int.TryParse(userId, out var result) ? result : throw new AuthenticationException("Invalid claim value."),
            Email = userEmail ?? throw new AuthenticationException("Invalid claim value.")
        };
    }
}
