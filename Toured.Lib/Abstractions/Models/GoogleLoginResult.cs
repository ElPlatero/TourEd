using System.Security.Claims;

namespace TourEd.Lib.Abstractions.Models;

public enum GoogleLoginStatus
{
    Authenticated,
    Pending,
    Rejected
}

public sealed record GoogleLoginResult(
    GoogleLoginStatus Status,
    User? User,
    RegistrationRequest? RegistrationRequest,
    ClaimsPrincipal? Principal);
