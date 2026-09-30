using System.Security.Claims;
using Api.Entities;

namespace Api.Authentication;

public interface IGoogleLoginService
{
    Task<User> AuthenticateAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default);
    Task<ClaimsPrincipal> CreatePrincipalAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default);
    Task<GoogleLoginResult> ProcessLoginAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default);
}
