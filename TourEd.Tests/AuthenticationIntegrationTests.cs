using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.Authentication;
using Api.Controllers.Auth;
using Api.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class AuthenticationIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-auth-tests-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-auth-keys-{Guid.NewGuid():N}");
    private TouredWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TouredWebApplicationFactory(_databasePath, _keysPath, useFakeGoogle: true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();
        context.Users.Add(new User { Email = FakeGoogleHandler.Email });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task FakeGoogleChallengeCallbackCreatesCookieAndRedirectsWithoutNetwork()
    {
        using var client = CreateClient(_factory);

        var challengeResponse = await client.GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.Redirect, challengeResponse.StatusCode);
        Assert.StartsWith("/fake-google/callback", challengeResponse.Headers.Location?.OriginalString);

        var callbackResponse = await client.GetAsync(challengeResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal("/", callbackResponse.Headers.Location?.OriginalString);
        var setCookie = Assert.Single(callbackResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("toured-session=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        var sessionJson = await client.GetStringAsync("/auth/session");
        using var sessionDocument = JsonDocument.Parse(sessionJson);
        var properties = sessionDocument.RootElement.EnumerateObject().ToList();
        Assert.Equal(2, properties.Count);
        Assert.True(sessionDocument.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.Equal(FakeGoogleHandler.Email, sessionDocument.RootElement.GetProperty("email").GetString());
        Assert.DoesNotContain("token", sessionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subject", sessionJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionDistinguishesAnonymousAndLogoutRemovesCookieSession()
    {
        using var client = CreateClient(_factory);
        var anonymousSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        Assert.NotNull(anonymousSession);
        Assert.False(anonymousSession.Authenticated);
        Assert.Null(anonymousSession.Email);

        await LoginAsync(client);
        var authenticatedSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        Assert.NotNull(authenticatedSession);
        Assert.True(authenticatedSession.Authenticated);

        var logoutResponse = await client.PostAsync("/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        var loggedOutSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        Assert.NotNull(loggedOutSession);
        Assert.False(loggedOutSession.Authenticated);
    }

    [Fact]
    public async Task CookieOptionsAreSecureHttpOnlyAndSameSiteLax()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TouredAuthenticationSchemes.Cookie);
        var googleOptions = _factory.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(TouredAuthenticationSchemes.Google);

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.True(options.SlidingExpiration);
        Assert.True(options.ExpireTimeSpan > TimeSpan.Zero);
        Assert.False(googleOptions.SaveTokens);
        Assert.Equal(TouredAuthenticationSchemes.Cookie, googleOptions.SignInScheme);
    }

    [Fact]
    public async Task PersistentDataProtectionKeysKeepSessionValidAcrossAppRestart()
    {
        using var firstClient = CreateClient(_factory);
        var callbackResponse = await LoginAsync(firstClient);
        var cookie = Assert.Single(callbackResponse.Headers.GetValues("Set-Cookie")).Split(';')[0];

        await using var restartedFactory = new TouredWebApplicationFactory(
            _databasePath,
            _keysPath,
            useFakeGoogle: true);
        using var restartedClient = CreateClient(restartedFactory, handleCookies: false);
        restartedClient.DefaultRequestHeaders.Add("Cookie", cookie);

        var session = await restartedClient.GetFromJsonAsync<AuthSessionResponse>("/auth/session");

        Assert.NotNull(session);
        Assert.True(session.Authenticated);
        Assert.Equal(FakeGoogleHandler.Email, session.Email);
    }

    [Fact]
    public async Task ProtectedAndUserFilteredApiRequestsReturnUnauthorizedWithoutRedirect()
    {
        using var client = CreateClient(_factory);

        var protectedResponse = await client.GetAsync("/api/points/1");
        var filteredResponse = await client.GetAsync("/api/points?vis=true");

        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Null(protectedResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.Unauthorized, filteredResponse.StatusCode);
        Assert.Null(filteredResponse.Headers.Location);
    }

    [Fact]
    public async Task LegacyHeaderAndCookieAuthenticationWorkInParallel()
    {
        using var legacyClient = CreateClient(_factory);
        legacyClient.DefaultRequestHeaders.Add(EmailHeaderAuthenticationOptions.HeaderName, FakeGoogleHandler.Email);

        var legacySession = await legacyClient.GetFromJsonAsync<AuthSessionResponse>("/auth/session");

        Assert.NotNull(legacySession);
        Assert.True(legacySession.Authenticated);
        Assert.Equal(FakeGoogleHandler.Email, legacySession.Email);
        Assert.Equal(HttpStatusCode.OK, (await legacyClient.GetAsync("/api/points?vis=false")).StatusCode);

        using var cookieClient = CreateClient(_factory);
        await LoginAsync(cookieClient);
        var cookieSession = await cookieClient.GetFromJsonAsync<AuthSessionResponse>("/auth/session");

        Assert.NotNull(cookieSession);
        Assert.True(cookieSession.Authenticated);
        Assert.Equal(FakeGoogleHandler.Email, cookieSession.Email);
        Assert.Equal(HttpStatusCode.OK, (await cookieClient.GetAsync("/api/points?vis=false")).StatusCode);
    }

    [Fact]
    public async Task PublicGoogleCallbackIncludesConfiguredPathBase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"toured-path-base-{Guid.NewGuid():N}.db");
        var keysPath = Path.Combine(Path.GetTempPath(), $"toured-path-base-keys-{Guid.NewGuid():N}");
        await using var factory = new TouredWebApplicationFactory(databasePath, keysPath, useFakeGoogle: false, pathBase: "/toured");
        try
        {
            using var client = CreateClient(factory);

            var response = await client.GetAsync("/toured/auth/login");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
            Assert.Equal("https://localhost/toured/signin-google", query["redirect_uri"]);
        }
        finally
        {
            DeleteFile(databasePath);
            DeleteDirectory(keysPath);
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        DeleteFile(_databasePath);
        DeleteDirectory(_keysPath);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool handleCookies = true)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies
        });

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        var challengeResponse = await client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, challengeResponse.StatusCode);
        var callbackResponse = await client.GetAsync(challengeResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        return callbackResponse;
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class TouredWebApplicationFactory(
        string databasePath,
        string keysPath,
        bool useFakeGoogle,
        string? pathBase = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TouredDb"] = $"Data Source={databasePath}",
                    ["Authentication:Google:ClientId"] = "test-client-id",
                    ["Authentication:Google:ClientSecret"] = "test-client-secret",
                    ["DataProtection:KeysPath"] = keysPath,
                    ["PathBase"] = pathBase
                }));

            if (useFakeGoogle)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeGoogleHandler>(
                        FakeGoogleHandler.AuthenticationSchemeName,
                        _ => { });
                    services.PostConfigure<PolicySchemeOptions>(
                        TouredAuthenticationSchemes.GoogleChallenge,
                        options => options.ForwardDefault = FakeGoogleHandler.AuthenticationSchemeName);
                });
            }
        }
    }

    private sealed class FakeGoogleHandler : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationRequestHandler
    {
        public const string AuthenticationSchemeName = "FakeGoogle";
        public const string Email = "known@example.test";
        private const string CallbackPath = "/fake-google/callback";

        public FakeGoogleHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var returnUrl = properties.RedirectUri ?? $"{Request.PathBase}/";
            var callback = $"{Request.PathBase}{CallbackPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";
            Response.Redirect(callback);
            return Task.CompletedTask;
        }

        public async Task<bool> HandleRequestAsync()
        {
            if (Request.Path != CallbackPath)
            {
                return false;
            }

            using var document = JsonDocument.Parse(
                $$"""{"id":"google-subject-1","email":"{{Email}}","verified_email":true}""");
            var ticketService = Context.RequestServices.GetRequiredService<GoogleOAuthTicketService>();
            var principal = await ticketService.CreatePrincipalAsync(document.RootElement, Context.RequestAborted);
            await Context.SignInAsync(TouredAuthenticationSchemes.Cookie, principal);
            Response.Redirect(Request.Query["returnUrl"].FirstOrDefault() ?? $"{Request.PathBase}/");
            return true;
        }
    }
}
