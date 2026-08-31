using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Dto;
using Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class AdminPointsIntegrationTests : IAsyncLifetime
{
    private const string CliToken = "test-only-admin-points-token-0123456789abcdef0123456789abcdef";
    private const string UserEmail = "admin-points-user@example.test";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-admin-points-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-admin-points-keys-{Guid.NewGuid():N}");
    private AdminWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new AdminWebApplicationFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();
        context.Users.Add(new User { Email = UserEmail });
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
    public async Task MissingOrInvalidTokenReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var payload = new[]
        {
            new AdminStampingPointRequestDto("touringen", "sonderstempel", null, "Sonderstempel Test", 50.5m, 11.2m, "sonderstempel-test", null, null)
        };

        var postNoAuth = await client.PostAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, postNoAuth.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        var postWrongAuth = await client.PostAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, postWrongAuth.StatusCode);

        var putWrongAuth = await client.PutAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, putWrongAuth.StatusCode);
    }

    [Fact]
    public async Task ValidTokenInsertsSonderstempelPointsSuccessfully()
    {
        using var client = CreateAuthorizedClient();
        var payload = new[]
        {
            new AdminStampingPointRequestDto(
                Provider: "touringen",
                Series: "sonderstempel",
                Number: null,
                Name: "Landesgartenschau Leinefelde-Worbis",
                Latitude: 51.385012m,
                Longitude: 10.325123m,
                ExternalId: "sonderstempel-lgs-worbis-2026",
                ValidFrom: new DateOnly(2026, 4, 23),
                ValidUntil: new DateOnly(2026, 10, 11)),
            new AdminStampingPointRequestDto(
                Provider: "touringen",
                Series: "sonderstempel",
                Number: null,
                Name: "Burgfest Hanstein",
                Latitude: 51.340123m,
                Longitude: 9.970456m,
                ExternalId: null,
                ValidFrom: new DateOnly(2026, 8, 1),
                ValidUntil: new DateOnly(2026, 8, 31))
        };

        var response = await client.PostAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AdminSavePointsResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Points.Count);

        var first = result.Points[0];
        Assert.True(first.Id > 0);
        Assert.Equal("touringen", first.Provider);
        Assert.Equal("sonderstempel", first.Series);
        Assert.Null(first.Number);
        Assert.Equal("Landesgartenschau Leinefelde-Worbis", first.Name);
        Assert.Equal(51.385012m, first.Latitude);
        Assert.Equal(10.325123m, first.Longitude);
        Assert.Equal("sonderstempel-lgs-worbis-2026", first.ExternalId);
        Assert.Equal(new DateOnly(2026, 4, 23), first.ValidFrom);
        Assert.Equal(new DateOnly(2026, 10, 11), first.ValidUntil);

        var second = result.Points[1];
        Assert.True(second.Id > 0);
        Assert.Equal("sonderstempel-burgfest-hanstein", second.ExternalId);

        // Verify points exist in database
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var dbPoints = await context.StampingPoints
            .Where(p => p.SeriesId == StampingSeries.TouringenSpecialStampsId)
            .OrderBy(p => p.Id)
            .ToListAsync();
        Assert.Equal(2, dbPoints.Count);
    }

    [Fact]
    public async Task UpsertUpdatesExistingPointAndPreservesVisits()
    {
        using var client = CreateAuthorizedClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var user = await context.Users.SingleAsync(u => u.Email == UserEmail);

        // Insert initial point
        var initial = new StampingPoint(
            0, "Alte Sonderstation", 10.5m, 50.8m, null, 0, StampingProvider.TouringenId, "sonder-test-station")
        {
            SeriesId = StampingSeries.TouringenSpecialStampsId
        };
        context.StampingPoints.Add(initial);
        await context.SaveChangesAsync();

        context.UserVisits.Add(new UserVisit
        {
            UserId = user.Id,
            StampingPointId = initial.Id,
            Visited = new DateTime(2026, 8, 15),
            HasVisitedTime = false,
            EntryCreated = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var initialId = initial.Id;

        // Upsert via admin endpoint to update name, coords, and validity
        var updatePayload = new[]
        {
            new AdminStampingPointRequestDto(
                Provider: "touringen",
                Series: "sonderstempel",
                Number: null,
                Name: "Aktualisierte Sonderstation",
                Latitude: 50.9m,
                Longitude: 10.6m,
                ExternalId: "sonder-test-station",
                ValidFrom: new DateOnly(2026, 5, 1),
                ValidUntil: new DateOnly(2026, 9, 30))
        };

        var response = await client.PutAsJsonAsync("/api/admin/points", updatePayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AdminSavePointsResponseDto>();
        Assert.NotNull(result);
        var updatedPoint = Assert.Single(result.Points);
        Assert.Equal(initialId, updatedPoint.Id);
        Assert.Equal("Aktualisierte Sonderstation", updatedPoint.Name);
        Assert.Equal(50.9m, updatedPoint.Latitude);
        Assert.Equal(10.6m, updatedPoint.Longitude);

        // Verify database and visit retention
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var dbPoint = await verifyContext.StampingPoints.SingleAsync(p => p.Id == initialId);
        Assert.Equal("Aktualisierte Sonderstation", dbPoint.Name);

        var visit = await verifyContext.UserVisits.SingleAsync(v => v.StampingPointId == initialId && v.UserId == user.Id);
        Assert.NotNull(visit.Visited);
    }

    [Theory]
    [InlineData("", 50.0, 10.0, "name is required")]
    [InlineData("Valid Name", -95.0, 10.0, "Invalid latitude")]
    [InlineData("Valid Name", 95.0, 10.0, "Invalid latitude")]
    [InlineData("Valid Name", 50.0, -190.0, "Invalid longitude")]
    [InlineData("Valid Name", 50.0, 190.0, "Invalid longitude")]
    public async Task ValidationRejectsInvalidCoordinatesAndNames(string name, double lat, double lon, string expectedError)
    {
        using var client = CreateAuthorizedClient();
        var payload = new[]
        {
            new AdminStampingPointRequestDto("touringen", "standard", 100, name, (decimal)lat, (decimal)lon, null, null, null)
        };

        var response = await client.PostAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(expectedError, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidationRejectsInvalidDateRange()
    {
        using var client = CreateAuthorizedClient();
        var payload = new[]
        {
            new AdminStampingPointRequestDto(
                "touringen", "sonderstempel", null, "Invalid Dates", 50.0m, 10.0m, null,
                new DateOnly(2026, 10, 1), new DateOnly(2026, 5, 1))
        };

        var response = await client.PostAsJsonAsync("/api/admin/points", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("cannot be after ValidUntil", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidationRejectsUnknownProviderOrSeries()
    {
        using var client = CreateAuthorizedClient();
        var unknownProviderPayload = new[]
        {
            new AdminStampingPointRequestDto("unknown-provider", "standard", 1, "Test", 50.0m, 10.0m, null, null, null)
        };
        var responseProvider = await client.PostAsJsonAsync("/api/admin/points", unknownProviderPayload);
        Assert.Equal(HttpStatusCode.BadRequest, responseProvider.StatusCode);

        var unknownSeriesPayload = new[]
        {
            new AdminStampingPointRequestDto("touringen", "nonexistent-series", 1, "Test", 50.0m, 10.0m, null, null, null)
        };
        var responseSeries = await client.PostAsJsonAsync("/api/admin/points", unknownSeriesPayload);
        Assert.Equal(HttpStatusCode.BadRequest, responseSeries.StatusCode);
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CliToken);
        return client;
    }

    private static void DeleteFileSafely(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void DeleteDirectorySafely(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class AdminWebApplicationFactory(string databasePath, string keysPath) : WebApplicationFactory<Program>
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
                    ["Authentication:Cli:Token"] = CliToken,
                    ["Authentication:Cli:UserEmail"] = UserEmail,
                    ["DataProtection:KeysPath"] = keysPath
                }));
        }
    }
}
