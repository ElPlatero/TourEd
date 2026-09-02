using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.Authentication;
using Api.Controllers.Points;
using Api.Controllers.Providers;
using Api.Dto;
using Api.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions.Models;
using Xunit;

namespace TourEd.Tests;

public sealed class ProviderProgressOverviewIntegrationTests : IAsyncLifetime
{
    private const int OtherProviderId = 10;
    private const string OtherProviderSlug = "progress-other";
    private const int UnreadyProviderId = 11;
    private const string UnreadyProviderSlug = "progress-unready";
    private const int NormalPointNumber = 1001;
    private const int TemporaryPointNumber = 1002;
    private const int ValidFromOnlyPointNumber = 1003;
    private const int ValidUntilOnlyPointNumber = 1004;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-progress-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-progress-keys-{Guid.NewGuid():N}");
    private ProgressWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ProgressWebApplicationFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();

        var otherProvider = new StampingProvider
        {
            Id = OtherProviderId,
            Name = "Other Test Provider",
            Abbreviation = "OTP",
            Slug = OtherProviderSlug,
            IsAnonymousAccessAllowed = true,
            Description = "Other provider description.",
            WebsiteUri = new Uri("https://provider.example.test/info"),
            DataSourceAttribution = "Provider test data",
            DataSourceUri = new Uri("https://provider.example.test/source"),
            DataLicenseName = "Test licence",
            DataLicenseUri = new Uri("https://provider.example.test/licence"),
            DataSourceRevision = "1",
            DataSourceUpdatedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            DataImportedAt = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
        };

        var unreadyProvider = new StampingProvider
        {
            Id = UnreadyProviderId,
            Name = "Unready Provider",
            Abbreviation = "UNR",
            Slug = UnreadyProviderSlug,
            IsAnonymousAccessAllowed = false,
            Description = "Unready provider description."
        };

        var otherSeries = new StampingSeries
        {
            Id = 20,
            ProviderId = OtherProviderId,
            Name = "Standard",
            Slug = "standard"
        };
        var unreadySeries = new StampingSeries
        {
            Id = 21,
            ProviderId = UnreadyProviderId,
            Name = "Standard",
            Slug = "standard"
        };

        var user = new User
        {
            Email = FakeGoogleHandler.Email,
            DefaultStampingProviderId = OtherProviderId
        };

        var normalPoint = new StampingPoint(
            default,
            "Permanent Point",
            11.8m,
            50.9m,
            NormalPointNumber,
            1,
            OtherProviderId,
            "ext-1")
        {
            SeriesId = otherSeries.Id
        };

        var temporaryPoint = new StampingPoint(
            default,
            "Temporary Point",
            11.85m,
            50.95m,
            TemporaryPointNumber,
            2,
            OtherProviderId,
            "ext-2")
        {
            SeriesId = otherSeries.Id,
            ValidFrom = new DateOnly(2026, 6, 1),
            ValidUntil = new DateOnly(2026, 8, 31)
        };

        var validFromOnlyPoint = new StampingPoint(
            default,
            "Valid From Only Point",
            11.86m,
            50.96m,
            ValidFromOnlyPointNumber,
            3,
            OtherProviderId,
            "ext-3")
        {
            SeriesId = otherSeries.Id,
            ValidFrom = new DateOnly(2026, 6, 1)
        };

        var validUntilOnlyPoint = new StampingPoint(
            default,
            "Valid Until Only Point",
            11.87m,
            50.97m,
            ValidUntilOnlyPointNumber,
            4,
            OtherProviderId,
            "ext-4")
        {
            SeriesId = otherSeries.Id,
            ValidUntil = new DateOnly(2026, 12, 31)
        };

        var unreadyPoint = new StampingPoint(
            default,
            "Unready Point",
            11.7m,
            50.8m,
            1,
            1,
            UnreadyProviderId,
            "ext-5")
        {
            SeriesId = unreadySeries.Id
        };

        context.StampingProviders.AddRange(otherProvider, unreadyProvider);
        context.StampingSeries.AddRange(otherSeries, unreadySeries);
        context.Users.Add(user);
        context.StampingPoints.AddRange(
            normalPoint,
            temporaryPoint,
            validFromOnlyPoint,
            validUntilOnlyPoint,
            unreadyPoint);
        await context.SaveChangesAsync();

        // Visit every ready-provider point so each validity shape is checked in both totals.
        context.UserVisits.AddRange(
            new UserVisit { UserId = user.Id, StampingPointId = normalPoint.Id, Visited = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc) },
            new UserVisit { UserId = user.Id, StampingPointId = temporaryPoint.Id, Visited = new DateTime(2026, 9, 1, 12, 30, 0, DateTimeKind.Utc) },
            new UserVisit { UserId = user.Id, StampingPointId = validFromOnlyPoint.Id, Visited = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc) },
            new UserVisit { UserId = user.Id, StampingPointId = validUntilOnlyPoint.Id, Visited = new DateTime(2026, 9, 1, 13, 30, 0, DateTimeKind.Utc) });

        // Grant access only to otherProvider initially
        context.UserStampingProviders.Add(new UserStampingProvider
        {
            UserId = user.Id,
            StampingProviderId = OtherProviderId
        });

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        DeleteFileSafely(_databasePath);
        DeleteDirectorySafely(_keysPath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnonymousProvidersRequestReturnsUnauthorized()
    {
        using var client = CreateClient(_factory);
        var response = await client.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedProvidersRequestReturnsFullCatalogWithProgressAndFlags()
    {
        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var response = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        Assert.NotNull(response);

        // All providers in DB are returned
        Assert.Contains(response.StampingProviders, p => p.Slug == StampingProvider.TouringenSlug);
        Assert.Contains(response.StampingProviders, p => p.Slug == OtherProviderSlug);
        Assert.Contains(response.StampingProviders, p => p.Slug == UnreadyProviderSlug);

        // Enabled provider checks (OtherProvider)
        var other = Assert.Single(response.StampingProviders, p => p.Slug == OtherProviderSlug);
        Assert.True(other.IsEnabled);
        Assert.True(other.IsDataReady);
        Assert.True(other.IsAnonymousAccessAllowed);
        Assert.True(other.HasPublicDataDownload);
        Assert.Equal(1, other.TotalPoints); // Temporary point excluded!
        Assert.Equal(1, other.VisitedPoints); // Temporary point excluded!

        // Unready provider checks
        var unready = Assert.Single(response.StampingProviders, p => p.Slug == UnreadyProviderSlug);
        Assert.False(unready.IsEnabled);
        Assert.False(unready.IsDataReady);
        Assert.False(unready.IsAnonymousAccessAllowed);
        Assert.False(unready.HasPublicDataDownload);
        Assert.Null(unready.TotalPoints);
        Assert.Null(unready.VisitedPoints);

        // Overall totals sum only enabled and data-ready providers
        Assert.Equal(other.TotalPoints!.Value, response.TotalPoints);
        Assert.Equal(other.VisitedPoints!.Value, response.VisitedPoints);
    }

    [Fact]
    public async Task LockedProviderReportsAccurateHistoricalProgressWhileHidingPointsAndGeoJson()
    {
        int userId;
        int normalPointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var user = await context.Users.SingleAsync(u => u.Email == FakeGoogleHandler.Email);
            var access = await context.UserStampingProviders.SingleAsync(a => a.UserId == user.Id && a.StampingProviderId == OtherProviderId);
            var point = await context.StampingPoints.SingleAsync(p => p.ProviderId == OtherProviderId && p.Number == NormalPointNumber);
            userId = user.Id;
            normalPointId = point.Id;

            // Remove entitlement for otherProvider
            context.UserStampingProviders.Remove(access);
            await context.SaveChangesAsync();
        }

        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var catalog = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        Assert.NotNull(catalog);

        var other = Assert.Single(catalog.StampingProviders, p => p.Slug == OtherProviderSlug);
        Assert.False(other.IsEnabled);
        Assert.True(other.IsDataReady);
        Assert.False(other.HasPublicDataDownload); // GeoJSON download hidden for locked provider
        Assert.Equal(1, other.TotalPoints);
        Assert.Equal(1, other.VisitedPoints); // Preserves historical count

        // Points, mutations, and GeoJSON are strictly forbidden
        var pointsResponse = await client.GetAsync($"/api/points?provider={OtherProviderSlug}");
        Assert.Equal(HttpStatusCode.Forbidden, pointsResponse.StatusCode);

        var singlePointResponse = await client.GetAsync($"/api/points/{NormalPointNumber}?provider={OtherProviderSlug}");
        Assert.Equal(HttpStatusCode.Forbidden, singlePointResponse.StatusCode);

        var stateResponse = await client.PutAsJsonAsync(
            $"/api/points/id/{normalPointId}/state?provider={OtherProviderSlug}",
            new SynchronizeVisitRequest(
                new VisitStateRequest(true, null, null),
                new VisitStateRequest(false, null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, stateResponse.StatusCode);

        var geoJsonResponse = await client.GetAsync($"/api/providers/{OtherProviderSlug}/points.geojson");
        Assert.Equal(HttpStatusCode.Forbidden, geoJsonResponse.StatusCode);
    }

    [Fact]
    public async Task EveryPointWithAnyValidityBoundaryIsExcludedFromProgress()
    {
        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var allPoints = await client.GetFromJsonAsync<GetStampingPointsResponse>($"/api/points?provider={OtherProviderSlug}");
        Assert.NotNull(allPoints);

        var perm = Assert.Single(allPoints.StampingPoints, p => p.Number == NormalPointNumber);
        Assert.True(perm.CountsTowardProgress);

        var temp = Assert.Single(allPoints.StampingPoints, p => p.Number == TemporaryPointNumber);
        Assert.False(temp.CountsTowardProgress);

        var validFromOnly = Assert.Single(allPoints.StampingPoints, p => p.Number == ValidFromOnlyPointNumber);
        Assert.False(validFromOnly.CountsTowardProgress);

        var validUntilOnly = Assert.Single(allPoints.StampingPoints, p => p.Number == ValidUntilOnlyPointNumber);
        Assert.False(validUntilOnly.CountsTowardProgress);
    }

    [Fact]
    public async Task UserWithNoEntitlementsReceivesFullCatalogWithZeroTotals()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var user = await context.Users.SingleAsync(u => u.Email == FakeGoogleHandler.Email);
            var allAccess = await context.UserStampingProviders.Where(a => a.UserId == user.Id).ToListAsync();
            context.UserStampingProviders.RemoveRange(allAccess);
            await context.SaveChangesAsync();
        }

        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var catalog = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog.StampingProviders);
        Assert.All(catalog.StampingProviders, p => Assert.False(p.IsEnabled));
        Assert.Equal(0, catalog.TotalPoints);
        Assert.Equal(0, catalog.VisitedPoints);
    }

    [Fact]
    public async Task BundledFrontendRendersProgressOverviewAndOmitsInfoButtonFromFilterList()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var js = await client.GetStringAsync("/js/toured.js");

        // HTML Progress Overview elements
        Assert.Contains("id=\"progressOverview\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressButtonCount\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressButtonFill\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressButtonSrPercent\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressPanel\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"progressList\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"closeProgressPanelButton\"", html, StringComparison.OrdinalIgnoreCase);

        // CSS styles and variables
        Assert.Contains("--progress-open: #279cdf;", css, StringComparison.Ordinal);
        Assert.Contains("--progress-visited: #123e65;", css, StringComparison.Ordinal);
        Assert.Contains(".progress-overview", css, StringComparison.Ordinal);
        Assert.Contains(".progress-button", css, StringComparison.Ordinal);
        Assert.Contains(".progress-bar", css, StringComparison.Ordinal);
        Assert.Contains(".progress-bar--summary", css, StringComparison.Ordinal);
        Assert.Contains(".progress-bar__fill", css, StringComparison.Ordinal);
        Assert.Contains(".progress-panel", css, StringComparison.Ordinal);
        Assert.Contains(".progress-item", css, StringComparison.Ordinal);
        Assert.Contains(".progress-item--locked", css, StringComparison.Ordinal);
        Assert.Contains(".progress-item--not-ready", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--progress-open);", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--progress-open-locked);", css, StringComparison.Ordinal);

        // JS logic
        Assert.Contains("SNAPSHOT_SCHEMA_VERSION = 3", js, StringComparison.Ordinal);
        Assert.Contains("renderProgressOverview", js, StringComparison.Ordinal);
        Assert.Contains("sortProvidersForProgressList", js, StringComparison.Ordinal);
        Assert.Contains("closeProgressMenu", js, StringComparison.Ordinal);
        Assert.Contains("toggleProgressMenu", js, StringComparison.Ordinal);
        Assert.Contains("calculateProgressStats", js, StringComparison.Ordinal);
        Assert.Contains("updateProgressSummaryAria", js, StringComparison.Ordinal);
        Assert.Contains("countsTowardProgress: point.countsTowardProgress === true", js, StringComparison.Ordinal);
        Assert.DoesNotContain("point.validFrom === null", js, StringComparison.Ordinal);
        Assert.Contains("updateProviderProgressInSnapshot", js, StringComparison.Ordinal);
        Assert.Contains("app.hasCompleteProviderCatalog", js, StringComparison.Ordinal);
        Assert.Contains("provider.isAnonymousAccessAllowed === true", js, StringComparison.Ordinal);
        Assert.Contains("Gesamtfortschritt ist offline noch nicht verfügbar", js, StringComparison.Ordinal);
        Assert.Contains("resetProviderCatalog();", js, StringComparison.Ordinal);
        Assert.Contains("trigger?.isConnected", js, StringComparison.Ordinal);
        Assert.Contains("createLockSvg", js, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", js, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(ProgressWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task LoginAsync(HttpClient client)
    {
        var challengeResponse = await client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, challengeResponse.StatusCode);
        var callbackUrl = challengeResponse.Headers.Location?.ToString();
        Assert.NotNull(callbackUrl);

        var callbackResponse = await client.GetAsync(callbackUrl);
        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
    }

    private static void DeleteFileSafely(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void DeleteDirectorySafely(string path)
    {
        if (Directory.Exists(path))
        {
            try { Directory.Delete(path, true); } catch { }
        }
    }

    private sealed class ProgressWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;
        private readonly string _keysPath;

        public ProgressWebApplicationFactory(string databasePath, string keysPath)
        {
            _databasePath = databasePath;
            _keysPath = keysPath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}",
                    ["DataProtection:KeysPath"] = _keysPath,
                    ["Authentication:Cli:UserEmail"] = "admin@example.test",
                    ["Authentication:Cli:Token"] = "test-token-12345",
                    ["Authentication:Google:ClientId"] = "google-client-id",
                    ["Authentication:Google:ClientSecret"] = "google-client-secret"
                });
            });

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

    private sealed class FakeGoogleHandler : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationRequestHandler
    {
        public const string AuthenticationSchemeName = "FakeGoogleProgress";
        public const string Email = "progress-user@example.test";
        private const string CallbackPath = "/fake-google-progress/callback";

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

            var email = Request.Query["email"].FirstOrDefault() ?? Email;
            var subject = Request.Query["subject"].FirstOrDefault() ?? "google-subject-progress";
            using var document = JsonDocument.Parse(
                $$"""{"id":"{{subject}}","email":"{{email}}","verified_email":true}""");
            var ticketService = Context.RequestServices.GetRequiredService<GoogleOAuthTicketService>();
            var result = await ticketService.ProcessTicketAsync(document.RootElement, Context.RequestAborted);
            if (result.Status == GoogleLoginStatus.Authenticated && result.Principal is not null)
            {
                await Context.SignInAsync(TouredAuthenticationSchemes.Cookie, result.Principal);
                Response.Redirect(Request.Query["returnUrl"].FirstOrDefault() ?? $"{Request.PathBase}/");
            }
            else
            {
                var outcome = result.Status == GoogleLoginStatus.Rejected ? "rejected" : "pending";
                Response.Redirect($"{Request.PathBase}/?registration={outcome}");
            }
            return true;
        }
    }
}
