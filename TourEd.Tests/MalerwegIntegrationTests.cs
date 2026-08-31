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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class MalerwegIntegrationTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-malerweg-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-malerweg-keys-{Guid.NewGuid():N}");
    private MalerwegWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new MalerwegWebApplicationFactory(_databasePath, _keysPath);
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

    [Fact]
    public async Task AnonymousPointsAndCatalogRequestsAreRejectedWithUnauthorized()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

        var pointsResponse = await client.GetAsync("/api/points?provider=malerweg");
        Assert.Equal(HttpStatusCode.Unauthorized, pointsResponse.StatusCode);

        var providersResponse = await client.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, providersResponse.StatusCode);

        var geoJsonResponse = await client.GetAsync("/api/providers/malerweg/points.geojson");
        Assert.Equal(HttpStatusCode.Unauthorized, geoJsonResponse.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserCanQueryMalerwegPointsAndCatalogWithoutProvenanceOrGeoJson()
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

        var response = await client.GetAsync("/api/points?provider=malerweg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(data);
        Assert.Equal(8, data.OverallCount);

        var ordered = data.StampingPoints.OrderBy(p => p.Number).ToArray();
        Assert.Equal(Enumerable.Range(1, 8), ordered.Select(p => p.Number!.Value));

        Assert.Equal("Liebethal", ordered[0].Name);
        Assert.Equal(50.9982441m, ordered[0].Position.Latitude);
        Assert.Equal(13.9538612m, ordered[0].Position.Longitude);
        Assert.Equal("standard", ordered[0].Series.Slug);

        Assert.Equal("Stadt Wehlen", ordered[1].Name);
        Assert.Equal("Hohnstein", ordered[2].Name);
        Assert.Equal("Brand", ordered[3].Name);
        Assert.Equal("Neumannmühle", ordered[4].Name);
        Assert.Equal("Großer Zschirnstein", ordered[5].Name);
        Assert.Equal("Gohrisch", ordered[6].Name);
        Assert.Equal("Rauenstein", ordered[7].Name);

        var providersResponse = await client.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.OK, providersResponse.StatusCode);

        var catalog = await providersResponse.Content.ReadFromJsonAsync<GetStampingProvidersResponse>();
        Assert.NotNull(catalog);

        var malerweg = Assert.Single(catalog.StampingProviders, p => p.Slug == StampingProvider.MalerwegSlug);
        Assert.Equal("Malerweg", malerweg.Name);
        Assert.Equal("MW", malerweg.Abbreviation);
        Assert.True(malerweg.IsAnonymousAccessAllowed);
        Assert.Equal("https://www.saechsische-schweiz.de/malerweg", malerweg.WebsiteUrl);
        Assert.Null(malerweg.DataSourceAttribution);
        Assert.Null(malerweg.DataLicenseUrl);
        Assert.Null(malerweg.DataSourceUrl);
        Assert.False(malerweg.HasPublicDataDownload);

        var geoJsonResponse = await client.GetAsync("/api/providers/malerweg/points.geojson");
        Assert.Equal(HttpStatusCode.NotFound, geoJsonResponse.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserCanVisitMalerwegPoints()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            await AddUserWithAccessAsync(context);
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

        // Put visit on Malerweg point 1
        var putResponse = await client.PutAsJsonAsync("/api/points/1?provider=malerweg", new
        {
            visitedOn = "2026-08-31",
            visitedAt = "10:30:00"
        });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        // Check visited points
        var visitedResponse = await client.GetAsync("/api/points?provider=malerweg&vis=true");
        Assert.Equal(HttpStatusCode.OK, visitedResponse.StatusCode);
        var visitedData = await visitedResponse.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(visitedData);
        var visited = Assert.Single(visitedData.StampingPoints);
        Assert.Equal(1, visited.Number);
        Assert.Equal("Liebethal", visited.Name);
        Assert.True(visited.IsVisited);

        // Check unvisited points
        var unvisitedResponse = await client.GetAsync("/api/points?provider=malerweg&vis=false");
        Assert.Equal(HttpStatusCode.OK, unvisitedResponse.StatusCode);
        var unvisitedData = await unvisitedResponse.Content.ReadFromJsonAsync<GetStampingPointsResponse>();
        Assert.NotNull(unvisitedData);
        Assert.Equal(7, unvisitedData.OverallCount);
        Assert.DoesNotContain(unvisitedData.StampingPoints, p => p.Number == 1);
    }

    private static async Task AddUserWithAccessAsync(DataContext context)
    {
        var user = new User { Email = FakeGoogleHandler.Email };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.UserStampingProviders.Add(new UserStampingProvider
        {
            UserId = user.Id,
            StampingProviderId = StampingProvider.MalerwegId
        });
        await context.SaveChangesAsync();
    }

    private static void DeleteFileSafely(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void DeleteDirectorySafely(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class MalerwegWebApplicationFactory(string databasePath, string keysPath) : WebApplicationFactory<Program>
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
        public const string AuthenticationSchemeName = "FakeGoogleMalerweg";
        public const string Email = "malerweg-hiker@example.test";
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
                $$"""{"id":"google-subject-malerweg","email":"{{Email}}","verified_email":true}""");
            var ticketService = Context.RequestServices.GetRequiredService<GoogleOAuthTicketService>();
            var principal = await ticketService.CreatePrincipalAsync(document.RootElement, Context.RequestAborted);
            await Context.SignInAsync(TouredAuthenticationSchemes.Cookie, principal);
            var returnUrl = Request.Query["returnUrl"].ToString();
            Response.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            return true;
        }
    }
}
