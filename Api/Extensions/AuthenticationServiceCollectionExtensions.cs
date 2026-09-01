using Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Extensions;

namespace Api.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    private const string RegistrationOutcomeProperty = "toured:registration-outcome";

    public static IServiceCollection AddTouredAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<GoogleOAuthTicketService>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = TouredAuthenticationSchemes.Cookie;
                options.DefaultAuthenticateScheme = TouredAuthenticationSchemes.Cookie;
                options.DefaultChallengeScheme = TouredAuthenticationSchemes.Cookie;
                options.DefaultForbidScheme = TouredAuthenticationSchemes.Cookie;
                options.DefaultSignInScheme = TouredAuthenticationSchemes.Cookie;
            })
            .AddCookie(TouredAuthenticationSchemes.Cookie, options =>
            {
                options.Cookie.Name = "toured-session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/auth/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnCheckSlidingExpiration = context =>
                {
                    var effectiveExpiresAt = context.ShouldRenew && context.Properties?.ExpiresUtc is { } currentExpiresAt
                        ? currentExpiresAt.Subtract(context.RemainingTime).Add(context.Options.ExpireTimeSpan)
                        : context.Properties?.ExpiresUtc;
                    if (effectiveExpiresAt is not null)
                    {
                        context.HttpContext.Items[TouredAuthenticationSchemes.EffectiveCookieExpiresAtItem] =
                            DateTimeOffset.FromUnixTimeSeconds(effectiveExpiresAt.Value.ToUnixTimeSeconds());
                    }

                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    if (context.Principal?.TryGetUser(out var claimedUser) != true)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var validClaimedUser = claimedUser!;
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    var storedUser = await userService.GetUserByIdOrDefaultAsync(
                        validClaimedUser.Id,
                        context.HttpContext.RequestAborted);
                    if (storedUser is null ||
                        !string.Equals(storedUser.Email, validClaimedUser.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(TouredAuthenticationSchemes.Cookie);
                    }
                };
                options.Events.OnRedirectToLogin = context => SetApiStatusOrRedirect(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => SetApiStatusOrRedirect(context, StatusCodes.Status403Forbidden);
            })
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
                        var result = await ticketService.ProcessTicketAsync(
                            context.User,
                            context.HttpContext.RequestAborted);

                        if (result.Status == GoogleLoginStatus.Authenticated && result.Principal is not null)
                        {
                            context.Principal = result.Principal;
                        }
                        else
                        {
                            var pathBase = context.HttpContext.Request.PathBase.Value ?? string.Empty;
                            var outcome = result.Status == GoogleLoginStatus.Rejected ? "rejected" : "pending";
                            context.Properties.Items[RegistrationOutcomeProperty] = outcome;
                            context.Properties.RedirectUri = $"{pathBase}/?registration={outcome}";
                        }
                    },
                    OnTicketReceived = context =>
                    {
                        if (context.Properties?.Items.TryGetValue(RegistrationOutcomeProperty, out var outcome) == true)
                        {
                            context.Properties.Items.Remove(RegistrationOutcomeProperty);
                            context.HandleResponse();
                            var pathBase = context.HttpContext.Request.PathBase.Value ?? string.Empty;
                            context.Response.Redirect($"{pathBase}/?registration={outcome}");
                        }

                        return Task.CompletedTask;
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
