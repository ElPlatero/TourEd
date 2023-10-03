using Microsoft.AspNetCore.Authentication;

namespace TourEd.Lib.Abstractions.Models;

public class EmailHeaderAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "TouredAuthenticationScheme";
    public const string HeaderName = "toured-user";
}