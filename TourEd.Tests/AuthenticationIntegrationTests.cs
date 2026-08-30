using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.Authentication;
using Api.Controllers.Auth;
using Api.Controllers.Points;
using Api.Dto;
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
    private const string RemovedUserHeader = "toured-user";
    private const int VisitedPointNumber = 101;
    private const int UnvisitedPointNumber = 102;
    private const int WritablePointNumber = 103;
    private const int OtherProviderPointNumber = 201;
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-auth-tests-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-auth-keys-{Guid.NewGuid():N}");
    private TouredWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TouredWebApplicationFactory(_databasePath, _keysPath, useFakeGoogle: true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();
        context.StampingProviders.Add(new StampingProvider
        {
            Id = 2,
            Slug = "other",
            Name = "Other provider"
        });
        var user = new User { Email = FakeGoogleHandler.Email };
        var visitedPoint = CreatePoint(VisitedPointNumber, StampingProvider.TouringenId);
        context.Users.Add(user);
        context.StampingPoints.AddRange(
            visitedPoint,
            CreatePoint(UnvisitedPointNumber, StampingProvider.TouringenId),
            CreatePoint(WritablePointNumber, StampingProvider.TouringenId),
            CreatePoint(OtherProviderPointNumber, 2));
        await context.SaveChangesAsync();
        context.UserVisits.Add(new UserVisit
        {
            UserId = user.Id,
            StampingPointId = visitedPoint.Id,
            Visited = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc)
        });
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
    public async Task RemovedHeaderAndUserIdQueryCannotAuthenticateWhileCookieStillWorks()
    {
        using var removedHeaderClient = CreateClient(_factory);
        removedHeaderClient.DefaultRequestHeaders.Add(RemovedUserHeader, FakeGoogleHandler.Email);

        var headerSession = await removedHeaderClient.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        var headerFilteredPoints = await removedHeaderClient.GetAsync("/api/points?vis=false");
        var headerVisit = await removedHeaderClient.GetAsync($"/api/points/{VisitedPointNumber}");
        var headerWrite = await removedHeaderClient.PutAsJsonAsync(
            $"/api/points/{WritablePointNumber}",
            new AddVisitRequest(true, DateTime.UtcNow));

        using var queryClient = CreateClient(_factory);
        var queryLogin = await queryClient.GetAsync(
            $"/api/points?vis=true&userid={Uri.EscapeDataString(FakeGoogleHandler.Email)}");

        Assert.NotNull(headerSession);
        Assert.False(headerSession.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, headerFilteredPoints.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, headerVisit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, headerWrite.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, queryLogin.StatusCode);

        using var cookieClient = CreateClient(_factory);
        await LoginAsync(cookieClient);
        var cookieSession = await cookieClient.GetFromJsonAsync<AuthSessionResponse>("/auth/session");

        Assert.NotNull(cookieSession);
        Assert.True(cookieSession.Authenticated);
        Assert.Equal(FakeGoogleHandler.Email, cookieSession.Email);
        Assert.Equal(HttpStatusCode.OK, (await cookieClient.GetAsync("/api/points?vis=false")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCookieWithoutInternalUserClaimsReturnsUnauthorized()
    {
        using var client = CreateClient(_factory, handleCookies: false);
        client.DefaultRequestHeaders.Add(
            "Cookie",
            CreateSessionCookie(_factory.Services, new ClaimsIdentity(authenticationType: TouredAuthenticationSchemes.Cookie)));

        var session = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        var filteredPoints = await client.GetAsync("/api/points?vis=true");
        var visit = await client.GetAsync($"/api/points/{VisitedPointNumber}");
        var write = await client.PutAsJsonAsync(
            $"/api/points/{WritablePointNumber}",
            new AddVisitRequest(true, DateTime.UtcNow));

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, filteredPoints.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task CookieIdentityControlsDefaultProviderAndVisitedFilters()
    {
        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var visitedResponse = await client.GetFromJsonAsync<GetStampingPointsResponse>("/api/points?vis=true");
        var unvisitedResponse = await client.GetFromJsonAsync<GetStampingPointsResponse>("/api/points?vis=false");

        Assert.NotNull(visitedResponse);
        Assert.NotNull(unvisitedResponse);
        var visitedPoints = visitedResponse.StampingPoints.ToArray();
        var unvisitedPoints = unvisitedResponse.StampingPoints.ToArray();
        Assert.Contains(visitedPoints, point => point.Number == VisitedPointNumber && point.Visited is not null);
        Assert.DoesNotContain(unvisitedPoints, point => point.Number == VisitedPointNumber);
        Assert.Contains(unvisitedPoints, point => point.Number == UnvisitedPointNumber && point.Visited is null);
        Assert.DoesNotContain(visitedPoints.Concat(unvisitedPoints), point => point.Number == OtherProviderPointNumber);
        Assert.All(visitedPoints.Concat(unvisitedPoints), point => Assert.Equal(StampingProvider.TouringenSlug, point.Provider.Slug));
    }

    [Fact]
    public async Task AnonymousPointsRemainAvailableWhileBothVisitedFiltersRequireAuthentication()
    {
        using var client = CreateClient(_factory);

        var anonymousResponse = await client.GetAsync("/api/points");
        var visitedResponse = await client.GetAsync("/api/points?vis=true");
        var unvisitedResponse = await client.GetAsync("/api/points?vis=false");

        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unvisitedResponse.StatusCode);
        Assert.Null(visitedResponse.Headers.Location);
        Assert.Null(unvisitedResponse.Headers.Location);
    }

    [Fact]
    public async Task CookieSessionCanWriteVisitWhileAnonymousRequestIsRejected()
    {
        var visited = new DateTime(2026, 8, 28, 14, 30, 0, DateTimeKind.Utc);
        using var anonymousClient = CreateClient(_factory);
        var anonymousResponse = await anonymousClient.PutAsJsonAsync(
            $"/api/points/{WritablePointNumber}",
            new AddVisitRequest(true, visited));

        using var cookieClient = CreateClient(_factory);
        await LoginAsync(cookieClient);
        var cookieResponse = await cookieClient.PutAsJsonAsync(
            $"/api/points/{WritablePointNumber}",
            new AddVisitRequest(true, visited));
        var storedVisit = await cookieClient.GetFromJsonAsync<VisitDto>($"/api/points/{WritablePointNumber}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cookieResponse.StatusCode);
        Assert.NotNull(storedVisit);
        Assert.Equal(visited, storedVisit.Visited);
    }

    [Fact]
    public async Task BundledFrontendUsesSessionRoutesWithoutUserIdentityOrTokenStorage()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var normalizedHtml = html.ToLowerInvariant();

        Assert.Contains("href=\"auth/login\"", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("auth/session", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("auth/logout", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("api/points?vis=false", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("api/points?vis=true", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("window.history.replacestate", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("userid", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("toured-user", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("localstorage", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionstorage", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", normalizedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("googlesubject", normalizedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivacyNoticeIsPublicLinkedAndExcludedFromSearchIndexing()
    {
        using var client = CreateClient(_factory);

        var frontend = await client.GetStringAsync("/");
        var privacyResponse = await client.GetAsync("/datenschutz/");
        var privacyNotice = await privacyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
        Assert.Contains("&copy; TourEd 2023 · <a class=\"privacy-link\" href=\"datenschutz/\">Datenschutz</a>", frontend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow, noarchive\"", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tino@schuettpelz.org", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Die Funktion „Abmelden“ beendet nur die aktuelle Sitzung", privacyNotice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FrontendSessionFlowSupportsAnonymousLoginVisitsLogoutAndAnonymousAgain()
    {
        using var client = CreateClient(_factory);

        var initialSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        var initialPoints = await client.GetAsync("/api/points");
        await LoginAsync(client);
        var authenticatedSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        var unvisitedPoints = await client.GetAsync("/api/points?vis=false");
        var visitedPoints = await client.GetAsync("/api/points?vis=true");
        var logout = await client.PostAsync("/auth/logout", content: null);
        var finalSession = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        var finalPoints = await client.GetAsync("/api/points");

        Assert.NotNull(initialSession);
        Assert.False(initialSession.Authenticated);
        Assert.Equal(HttpStatusCode.OK, initialPoints.StatusCode);
        Assert.NotNull(authenticatedSession);
        Assert.True(authenticatedSession.Authenticated);
        Assert.Equal(HttpStatusCode.OK, unvisitedPoints.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visitedPoints.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.NotNull(finalSession);
        Assert.False(finalSession.Authenticated);
        Assert.Equal(HttpStatusCode.OK, finalPoints.StatusCode);
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
            var privacyResponse = await client.GetAsync("/toured/datenschutz/");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
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

    private static StampingPoint CreatePoint(int number, int providerId)
        => new(
            default,
            $"Point {number}",
            11.8m,
            50.9m,
            number,
            number,
            providerId,
            $"test-{providerId}-{number}");

    private static string CreateSessionCookie(IServiceProvider services, ClaimsIdentity identity)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TouredAuthenticationSchemes.Cookie);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TouredAuthenticationSchemes.Cookie);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
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
