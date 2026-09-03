using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.Authentication;
using Api.Controllers.Points;
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

public sealed class SeededTrailProvidersIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-seeded-trails-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-seeded-trails-keys-{Guid.NewGuid():N}");
    private SeededTrailsWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new SeededTrailsWebApplicationFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        DeleteFileSafely(_databasePath);
        DeleteDirectorySafely(_keysPath);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("schluchtensteig")]
    [InlineData("heidschnuckenweg")]
    [InlineData("harzer-klosterwanderweg")]
    [InlineData("bliessteig")]
    public async Task AnonymousPointsAndCatalogRequestsAreRejectedWithUnauthorized(string slug)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

        var pointsResponse = await client.GetAsync($"/api/points?provider={slug}");
        Assert.Equal(HttpStatusCode.Unauthorized, pointsResponse.StatusCode);

        var geoJsonResponse = await client.GetAsync($"/api/providers/{slug}/points.geojson");
        Assert.Equal(HttpStatusCode.Unauthorized, geoJsonResponse.StatusCode);
    }

    [Theory]
    [InlineData("schluchtensteig", "Schluchtensteig", "SST", 6, false)]
    [InlineData("heidschnuckenweg", "Heidschnuckenweg", "HNW", 13, false)]
    [InlineData("harzer-klosterwanderweg", "Harzer Klosterwanderweg", "HKW", 16, false)]
    [InlineData("bliessteig", "Bliessteig", "BS", 10, true)]
    public async Task AuthenticatedUserCanQuerySeededTrailProviderPoints(
        string slug,
        string expectedName,
        string expectedAbbr,
        int expectedCount,
        bool hasPublicDataDownload)
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            if (!await context.Users.AnyAsync(u => u.Email == FakeGoogleHandler.Email))
            {
                await AddUserWithAccessAsync(context);
            }
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var challenge = await client.GetAsync("/auth/login");
        var callback = await client.GetAsync(challenge.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        var response = await client.GetAsync($"/api/points?provider={slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(data);
        Assert.Equal(expectedCount, data.OverallCount);

        var ordered = data.StampingPoints.OrderBy(p => p.Number).ToArray();
        Assert.Equal(Enumerable.Range(1, expectedCount), ordered.Select(p => p.Number!.Value));

        var firstPoint = ordered[0];
        Assert.Equal(slug, firstPoint.Provider.Slug);
        Assert.Equal(expectedName, firstPoint.Provider.Name);
        Assert.Equal(expectedAbbr, firstPoint.Provider.Abbreviation);
        Assert.Equal(1, firstPoint.Number);
        Assert.False(firstPoint.IsVisited);
        Assert.Null(firstPoint.VisitedOn);
        Assert.Null(firstPoint.VisitedAt);
        Assert.Equal("standard", firstPoint.Series.Slug);

        var geoJsonResponse = await client.GetAsync($"/api/providers/{slug}/points.geojson");
        Assert.Equal(hasPublicDataDownload ? HttpStatusCode.OK : HttpStatusCode.NotFound, geoJsonResponse.StatusCode);
    }

    [Theory]
    [InlineData("schluchtensteig", 6)]
    [InlineData("heidschnuckenweg", 13)]
    [InlineData("harzer-klosterwanderweg", 16)]
    [InlineData("bliessteig", 10)]
    public async Task AuthenticatedUserCanVisitAndManageSeededPoints(string slug, int totalPoints)
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            if (!await context.Users.AnyAsync(u => u.Email == FakeGoogleHandler.Email))
            {
                await AddUserWithAccessAsync(context);
            }
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var challenge = await client.GetAsync("/auth/login");
        var callback = await client.GetAsync(challenge.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        // Put visit on point 1
        var putResponse = await client.PutAsJsonAsync($"/api/points/1?provider={slug}", new
        {
            visitedOn = "2026-08-31",
            visitedAt = "12:00:00"
        });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        // Query visited points
        var visitedResponse = await client.GetAsync($"/api/points?provider={slug}&vis=true");
        Assert.Equal(HttpStatusCode.OK, visitedResponse.StatusCode);
        var visitedData = await visitedResponse.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(visitedData);
        var visited = Assert.Single(visitedData.StampingPoints);
        Assert.Equal(1, visited.Number);
        Assert.True(visited.IsVisited);
        Assert.Equal(new DateOnly(2026, 8, 31), visited.VisitedOn);
        Assert.Equal(new TimeOnly(12, 0, 0), visited.VisitedAt);

        // Query unvisited points
        var unvisitedResponse = await client.GetAsync($"/api/points?provider={slug}&vis=false");
        Assert.Equal(HttpStatusCode.OK, unvisitedResponse.StatusCode);
        var unvisitedData = await unvisitedResponse.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(unvisitedData);
        Assert.Equal(totalPoints - 1, unvisitedData.StampingPoints.Count());

        // Delete visit
        var deleteResponse = await client.DeleteAsync($"/api/points/1?provider={slug}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Confirm visited is now 0
        var visitedAfterDelete = await client.GetAsync($"/api/points?provider={slug}&vis=true");
        var visitedAfterDeleteData = await visitedAfterDelete.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(visitedAfterDeleteData);
        Assert.Empty(visitedAfterDeleteData.StampingPoints);
    }

    [Fact]
    public async Task BliessteigSeedUsesOfficialStageEndpointsAndCcByProvenance()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var provider = await context.StampingProviders.SingleAsync(
            item => item.Id == StampingProvider.BliessteigId);
        Assert.Equal("bliessteig", provider.Slug);
        Assert.Equal("Saarpfalz-Touristik, Julia Serov", provider.DataSourceAttribution);
        Assert.Equal("Creative Commons Namensnennung 4.0 International (CC BY 4.0)", provider.DataLicenseName);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", provider.DataLicenseUri?.AbsoluteUri);
        Assert.NotNull(provider.DataImportedAt);

        var points = await context.StampingPoints
            .Where(point => point.ProviderId == StampingProvider.BliessteigId)
            .OrderBy(point => point.Number)
            .ToArrayAsync();

        Assert.Equal(
            [
                "Sarreguemines Bahnhof",
                "Gräfinthal",
                "Bebelsheim",
                "Blieskastel",
                "Kirkel",
                "Schwarzenacker",
                "Homburg",
                "Jägersburg",
                "Höchen",
                "Kulturbahnhof Bexbach"
            ],
            points.Select(point => point.Name));
        Assert.Equal(7.072924m, points[0].Longitude);
        Assert.Equal(49.110405m, points[0].Latitude);
        Assert.Equal(7.254470m, points[^1].Longitude);
        Assert.Equal(49.346269m, points[^1].Latitude);
    }

    private static async Task AddUserWithAccessAsync(DataContext context)
    {
        var user = new User { Email = FakeGoogleHandler.Email };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.UserStampingProviders.AddRange(new[]
        {
            StampingProvider.SchluchtensteigId,
            StampingProvider.HeidschnuckenwegId,
            StampingProvider.HarzerKlosterwanderwegId,
            StampingProvider.BliessteigId
        }.Select(providerId => new UserStampingProvider
        {
            UserId = user.Id,
            StampingProviderId = providerId
        }));
        await context.SaveChangesAsync();
    }

    private static void DeleteFileSafely(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    private static void DeleteDirectorySafely(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            // best effort
        }
    }

    private sealed class SeededTrailsWebApplicationFactory(string databasePath, string keysPath) : WebApplicationFactory<Program>
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
                    ["DataProtection:KeysPath"] = keysPath
                }));

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

    private sealed class FakeGoogleHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationRequestHandler
    {
        public const string AuthenticationSchemeName = "FakeGoogleSeededTrails";
        public const string Email = "trails-hiker@example.test";
        private const string CallbackPath = "/fake-google/callback";

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
                $$"""{"id":"google-subject-seeded-trails","email":"{{Email}}","verified_email":true}""");
            var ticketService = Context.RequestServices.GetRequiredService<GoogleOAuthTicketService>();
            var principal = await ticketService.CreatePrincipalAsync(document.RootElement, Context.RequestAborted);
            await Context.SignInAsync(TouredAuthenticationSchemes.Cookie, principal);
            var returnUrl = Request.Query["returnUrl"].ToString();
            Response.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            return true;
        }
    }
}
