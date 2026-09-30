using System.Security.Claims;
using Api.Entities;

namespace Api.Authentication;

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
