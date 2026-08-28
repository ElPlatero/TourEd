using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Managers;
using Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ProviderMigrationsCanBeAppliedToEmptyDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        await using var context = new DataContext(configuration);

        await context.Database.MigrateAsync();

        var migrations = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains("20260626000000_AddStampingProvider", migrations);
        Assert.Contains("20260626010000_AddProviderFieldsToStampingPoints", migrations);
        Assert.Equal(StampingProvider.TouringenSlug, (await context.StampingProviders.SingleAsync()).Slug);
    }

    [Fact]
    public async Task ProviderMigrationsPreserveExistingUsersAndStampingPoints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        await using var context = new DataContext(configuration);
        await context.Database.MigrateAsync("20231014145354_AddForeignKeyToSortedStampingPoint");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email) VALUES (7, 'existing@example.test');");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO StampingPoints (Id, Name, Longitude, Latitude, Number, Code) " +
            "VALUES (42, 'Existing point', '11.5', '50.5', 123, 456);");

        await context.Database.MigrateAsync();

        var user = await context.Users.AsNoTracking().SingleAsync();
        var point = await context.StampingPoints.AsNoTracking().SingleAsync();
        Assert.Equal(7, user.Id);
        Assert.Equal(StampingProvider.TouringenId, user.DefaultStampingProviderId);
        Assert.Equal(42, point.Id);
        Assert.Equal(StampingProvider.TouringenId, point.ProviderId);
        Assert.Equal("42", point.ExternalId);
    }

    [Fact]
    public async Task ProviderMigrationsNormalizeDuplicateStampingPointNumbers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        await using var context = new DataContext(configuration);
        await context.Database.MigrateAsync("20231014145354_AddForeignKeyToSortedStampingPoint");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email) VALUES (7, 'existing@example.test');");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO StampingPoints (Id, Name, Longitude, Latitude, Number, Code) VALUES " +
            "(42, 'Existing point', '11.5', '50.5', 123, 456), " +
            "(99, 'Existing point', '11.6', '50.6', 123, 456);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserVisit (Id, UserId, Visited, StampingPointId) VALUES " +
            "(100, 7, '2026-08-28 10:00:00', 42), " +
            "(101, 7, '2026-08-28 10:00:00', 99);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO HikingTours (Id, Name, IsKidsTour, IsCircularPath, IsLongDistanceTrail) VALUES " +
            "(10, 'First tour', 0, 0, 0), " +
            "(11, 'Second tour', 0, 0, 0);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO SortedStampingPoint (Position, StampingPointId, TourId) VALUES " +
            "(1, 42, 10), " +
            "(2, 99, 11);");

        await context.Database.MigrateAsync();

        var point = await context.StampingPoints.AsNoTracking().SingleAsync();
        Assert.Equal(99, point.Id);
        Assert.Equal(StampingProvider.TouringenId, point.ProviderId);
        Assert.Equal("99", point.ExternalId);

        var visit = await context.UserVisits.AsNoTracking().SingleAsync();
        Assert.Equal(99, visit.StampingPointId);

        var tourPointIds = await context.StampingPointsInTours.AsNoTracking()
            .Select(p => p.StampingPointId)
            .ToListAsync();
        Assert.Equal(2, tourPointIds.Count);
        Assert.All(tourPointIds, id => Assert.Equal(99, id));
    }

    [Fact]
    public void TouringenAdapterUsesProviderScopedExternalId()
    {
        var rawPoint = CreateRawStampPoint(9_001, 42);

        var point = rawPoint.CreateStampingPoint();

        Assert.Equal(default, point.Id);
        Assert.Equal(StampingProvider.TouringenId, point.ProviderId);
        Assert.Equal("9001", point.ExternalId);
    }

    [Fact]
    public async Task SavingPointsUsesProviderAndNumberAsImportIdentity()
    {
        await using var context = await CreateContextAsync();
        context.StampingProviders.Add(CreateProvider(2, "other"));
        await context.SaveChangesAsync();
        var repository = new TouredRepository(context);

        var savedPoints = await repository.SaveStampingPointsAsync(
            CreatePoint("Touringen", StampingProvider.TouringenId, "shared", 42),
            CreatePoint("Other", 2, "shared", 42));

        Assert.Equal(2, savedPoints.Count);
        Assert.All(savedPoints, point => Assert.True(point.Id > 0));
        Assert.NotEqual(savedPoints[0].Id, savedPoints[1].Id);

        var updatedPoint = CreatePoint("Touringen updated", StampingProvider.TouringenId, "updated", 42) with { Id = 99_999 };
        var updated = Assert.Single(await repository.SaveStampingPointsAsync(updatedPoint));

        Assert.Equal(savedPoints[0].Id, updated.Id);
        Assert.Equal(2, await context.StampingPoints.CountAsync());
        Assert.Equal("Touringen updated", (await context.StampingPoints.SingleAsync(p => p.Id == updated.Id)).Name);
        Assert.Equal("updated", (await context.StampingPoints.SingleAsync(p => p.Id == updated.Id)).ExternalId);
    }

    [Fact]
    public async Task UserImportUsesUsersDefaultProvider()
    {
        await using var context = await CreateContextAsync();
        context.StampingProviders.Add(CreateProvider(2, "other"));
        await context.SaveChangesAsync();

        var user = new User { Email = "user@example.test", DefaultStampingProviderId = 2 };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new TouredRepository(context);
        var points = await repository.SaveStampingPointsAsync(
            CreatePoint("Touringen", StampingProvider.TouringenId, "touringen-42", 42),
            CreatePoint("Other", 2, "other-42", 42));
        var otherPoint = points.Single(p => p.ProviderId == 2);
        var manager = CreateImportManager(context, repository, user, null);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("42;01.02.2026;12:30"));

        await manager.ImportUserDataAsync(stream);

        var visit = Assert.Single(await context.UserVisits.AsNoTracking().ToListAsync());
        Assert.Equal(otherPoint.Id, visit.StampingPointId);
        Assert.Equal(new DateTime(2026, 2, 1, 12, 30, 0), visit.Visited);
    }

    [Fact]
    public async Task TouringenImportMapsTourRelationsToGeneratedPointIds()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var firstRawPoint = CreateRawStampPoint(9_001, 42);
        var secondRawPoint = CreateRawStampPoint(9_002, 42);
        var firstRawTour = new RawTour(101, "First tour", [firstRawPoint], false, true, false, null, "Start", "End");
        var secondRawTour = new RawTour(102, "Second tour", [secondRawPoint], false, true, false, null, "Start", "End");
        var rawData = JsonSerializer.Serialize(new[] { new RawArea(1, "Area", [firstRawTour, secondRawTour], []) });
        var manager = CreateImportManager(context, repository, null, rawData);

        await manager.ImportTouringenDataAsync();

        var point = await context.StampingPoints.AsNoTracking().SingleAsync();
        var relations = await context.StampingPointsInTours.AsNoTracking().ToListAsync();
        Assert.NotEqual(secondRawPoint.Id, point.Id);
        Assert.Equal(secondRawPoint.Id.ToString(), point.ExternalId);
        Assert.Equal(2, relations.Count);
        Assert.All(relations, relation => Assert.Equal(point.Id, relation.StampingPointId));
    }

    private async Task<DataContext> CreateContextAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        var context = new DataContext(configuration);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static ImportManager CreateImportManager(DataContext context, TouredRepository repository, User? user, string? rawData)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(Constants.ClaimsNames.UserId, user.Id.ToString()),
                new Claim(Constants.ClaimsNames.UserEmail, user.Email)
            ], "test"));
        }

        return new ImportManager(
            new HttpContextAccessor { HttpContext = httpContext },
            new StubHtmlParsingService(rawData),
            Options.Create(new TouringenWebsiteConfiguration { StempelstellenUri = new Uri("https://example.test/stamping-points") }),
            new StampingPointImportService(),
            new HikingToursImportService(),
            repository);
    }

    private static RawStampPoint CreateRawStampPoint(int externalId, int number)
        => new(externalId, $"Point {number}", 50.0m, 11.0m, 1, number, number * 10, $"Point {number}");

    private static StampingProvider CreateProvider(int id, string slug)
        => new() { Id = id, Slug = slug, Name = slug };

    private static StampingPoint CreatePoint(string name, int providerId, string externalId, int number)
        => new(default, name, 11.0m, 50.0m, number, number * 10, providerId, externalId);

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class StubHtmlParsingService(string? rawData) : IHtmlParsingService
    {
        public Task<string?> GetRawDmoStringAsync(Uri uri) => Task.FromResult(rawData);
    }
}
