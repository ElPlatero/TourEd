using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using System.Security.Claims;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static User GetUser(this ClaimsPrincipal principal)
    {
        return principal.TryGetUser(out var user)
            ? user
            : throw new AuthenticationException("Missing or invalid user claims.");
    }

    public static bool TryGetUser(this ClaimsPrincipal principal, [NotNullWhen(true)] out User? user)
    {
        var userId = principal.FindFirst(Constants.ClaimsNames.UserId)?.Value;
        var userEmail = principal.FindFirst(Constants.ClaimsNames.UserEmail)?.Value;
        if (!int.TryParse(userId, out var parsedUserId) || parsedUserId <= 0 || string.IsNullOrWhiteSpace(userEmail))
        {
            user = null;
            return false;
        }

        user = new User { Id = parsedUserId, Email = userEmail };
        return true;
    }
}
