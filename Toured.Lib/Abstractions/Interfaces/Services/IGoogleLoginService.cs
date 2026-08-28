using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IGoogleLoginService
{
    Task<User> AuthenticateAsync(GoogleLoginClaims claims, CancellationToken cancellationToken = default);
}
