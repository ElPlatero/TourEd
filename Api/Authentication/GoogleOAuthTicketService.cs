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
        var claims = ExtractClaims(googleUser);
        return _loginService.CreatePrincipalAsync(claims, cancellationToken);
    }

    public Task<GoogleLoginResult> ProcessTicketAsync(JsonElement googleUser, CancellationToken cancellationToken = default)
    {
        var claims = ExtractClaims(googleUser);
        return _loginService.ProcessLoginAsync(claims, cancellationToken);
    }

    private static GoogleLoginClaims ExtractClaims(JsonElement googleUser)
        => new(
            GetString(googleUser, "sub") ?? GetString(googleUser, "id") ?? string.Empty,
            GetString(googleUser, "email") ?? string.Empty,
            GetBoolean(googleUser, "email_verified") ?? GetBoolean(googleUser, "verified_email") ?? false);

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
