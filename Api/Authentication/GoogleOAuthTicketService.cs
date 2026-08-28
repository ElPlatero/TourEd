using System.Security.Claims;
using System.Text.Json;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace Api.Authentication;

public sealed class GoogleOAuthTicketService
{
    private readonly IGoogleLoginService _loginService;

    public GoogleOAuthTicketService(IGoogleLoginService loginService)
    {
        _loginService = loginService;
    }

    public Task<ClaimsPrincipal> CreatePrincipalAsync(JsonElement googleUser, CancellationToken cancellationToken = default)
    {
        var claims = new GoogleLoginClaims(
            GetString(googleUser, "sub") ?? GetString(googleUser, "id") ?? string.Empty,
            GetString(googleUser, "email") ?? string.Empty,
            GetBoolean(googleUser, "email_verified") ?? GetBoolean(googleUser, "verified_email") ?? false);

        return _loginService.CreatePrincipalAsync(claims, cancellationToken);
    }

    private static string? GetString(JsonElement user, string propertyName)
        => user.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? GetBoolean(JsonElement user, string propertyName)
        => user.TryGetProperty(propertyName, out var property) &&
           (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;
}
