using Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Services;

namespace Api.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddTouredAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<GoogleOAuthTicketService>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = TouredAuthenticationSchemes.Application;
                options.DefaultAuthenticateScheme = TouredAuthenticationSchemes.Application;
                options.DefaultChallengeScheme = TouredAuthenticationSchemes.Application;
                options.DefaultForbidScheme = TouredAuthenticationSchemes.Application;
                options.DefaultSignInScheme = TouredAuthenticationSchemes.Cookie;
            })
            .AddPolicyScheme(
                TouredAuthenticationSchemes.Application,
                TouredAuthenticationSchemes.Application,
                options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(EmailHeaderAuthenticationOptions.HeaderName)
                        ? EmailHeaderAuthenticationOptions.DefaultScheme
                        : TouredAuthenticationSchemes.Cookie)
            .AddCookie(TouredAuthenticationSchemes.Cookie, options =>
            {
                options.Cookie.Name = "toured-session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/auth/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context => SetApiStatusOrRedirect(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => SetApiStatusOrRedirect(context, StatusCodes.Status403Forbidden);
            })
            .AddScheme<EmailHeaderAuthenticationOptions, TouredAuthenticationHandler>(
                EmailHeaderAuthenticationOptions.DefaultScheme,
                _ => { })
            .AddScheme<CliBearerAuthenticationOptions, CliBearerAuthenticationHandler>(
                TouredAuthenticationSchemes.CliBearer,
                options => configuration.GetSection(CliBearerAuthenticationOptions.ConfigurationSectionName).Bind(options))
            .AddPolicyScheme(
                TouredAuthenticationSchemes.GoogleChallenge,
                TouredAuthenticationSchemes.GoogleChallenge,
                options => options.ForwardDefault = TouredAuthenticationSchemes.Google)
            .AddGoogle(TouredAuthenticationSchemes.Google, options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
                options.SignInScheme = TouredAuthenticationSchemes.Cookie;
                options.SaveTokens = false;
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        var ticketService = context.HttpContext.RequestServices.GetRequiredService<GoogleOAuthTicketService>();
                        context.Principal = await ticketService.CreatePrincipalAsync(
                            context.User,
                            context.HttpContext.RequestAborted);
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options => options.AddPolicy(
            TouredAuthorizationPolicies.CliImport,
            policy =>
            {
                policy.AddAuthenticationSchemes(TouredAuthenticationSchemes.CliBearer);
                policy.RequireAuthenticatedUser();
            }));

        return services;
    }

    public static IServiceCollection AddTouredDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredPath = configuration["DataProtection:KeysPath"];
        var keysPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TourEd",
                "DataProtection-Keys")
            : configuredPath;

        if (!Path.IsPathFullyQualified(keysPath))
        {
            throw new InvalidOperationException("DataProtection:KeysPath must be an absolute path.");
        }

        var keysDirectory = Directory.CreateDirectory(keysPath);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                keysDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        services.AddDataProtection()
            .SetApplicationName("TourEd")
            .PersistKeysToFileSystem(keysDirectory);

        return services;
    }

    private static Task SetApiStatusOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }
}
