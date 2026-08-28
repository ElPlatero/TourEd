using Microsoft.AspNetCore.Authentication;

namespace Api.Authentication;

public sealed class CliBearerAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string ConfigurationSectionName = "Authentication:Cli";

    public string? Token { get; set; }
    public string? UserEmail { get; set; }
}
