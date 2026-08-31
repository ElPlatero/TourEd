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
        Assert.Contains("20260830132352_UpdateStampingProviderMetadata", migrations);
        Assert.Contains("20260830150127_AddHarzerWandernadelProvider", migrations);
        Assert.Contains("20260830184653_SupportOptionalVisitTimestamps", migrations);
        Assert.Contains("20260830203007_AddStampingProviderDataSourceMetadata", migrations);
        Assert.Contains("20260831112919_AddStampingSeries", migrations);
        Assert.Contains("20260831133503_AddMalerwegProvider", migrations);
        var providers = await context.StampingProviders.OrderBy(provider => provider.Id).ToArrayAsync();
        Assert.Equal(3, providers.Length);
        Assert.Equal(StampingProvider.TouringenSlug, providers[0].Slug);
        Assert.True(providers[0].IsAnonymousAccessAllowed);
        Assert.Contains("430 offizielle Stempelstellen", providers[0].Description, StringComparison.Ordinal);
        Assert.Equal(StampingProvider.HarzerWandernadelSlug, providers[1].Slug);
        Assert.Equal("HWN", providers[1].Abbreviation);
        Assert.False(providers[1].IsAnonymousAccessAllowed);
        Assert.Equal(StampingProvider.MalerwegSlug, providers[2].Slug);
        Assert.Equal("MW", providers[2].Abbreviation);
        Assert.True(providers[2].IsAnonymousAccessAllowed);
        var series = await context.StampingSeries.OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(6, series.Length);
        Assert.Equal(430, series.Single(item => item.Id == StampingSeries.TouringenStandardId).ExpectedPointCount);
        Assert.True(series.Single(item => item.Id == StampingSeries.TouringenSpecialStampsId).IsTemporary);
        Assert.Equal(8, series.Single(item => item.Id == StampingSeries.MalerwegStandardId).ExpectedPointCount);
    }

    [Fact]
    public async Task ProviderMetadataMigrationPreservesCustomizedDescription()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        await using var context = new DataContext(configuration);
        await context.Database.MigrateAsync("20260828181042_AddGoogleSubjectToUsers");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE StampingProviders SET Description = 'Locally customized.' WHERE Id = {0};",
            StampingProvider.TouringenId);

        await context.Database.MigrateAsync();

        Assert.Equal("Locally customized.", await context.StampingProviders
            .Where(provider => provider.Id == StampingProvider.TouringenId)
            .Select(provider => provider.Description)
            .SingleAsync());
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
        var point = await context.StampingPoints.AsNoTracking().SingleAsync(p => p.Id == 42);
        Assert.Equal(7, user.Id);
        Assert.Equal(StampingProvider.TouringenId, user.DefaultStampingProviderId);
        Assert.Equal(42, point.Id);
        Assert.Equal(StampingProvider.TouringenId, point.ProviderId);
        Assert.Equal("42", point.ExternalId);
        Assert.Equal(StampingSeries.TouringenStandardId, point.SeriesId);
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

        var point = await context.StampingPoints.AsNoTracking().SingleAsync(p => p.ProviderId == StampingProvider.TouringenId);
        Assert.Equal(99, point.Id);
        Assert.Equal(StampingProvider.TouringenId, point.ProviderId);
        Assert.Equal("99", point.ExternalId);

        var visit = await context.UserVisits.AsNoTracking().SingleAsync();
        Assert.Equal(99, visit.StampingPointId);
        Assert.True(visit.HasVisitedTime);

        var tourPointIds = await context.StampingPointsInTours.AsNoTracking()
            .Select(p => p.StampingPointId)
            .ToListAsync();
        Assert.Equal(2, tourPointIds.Count);
        Assert.All(tourPointIds, id => Assert.Equal(99, id));
    }

    [Fact]
    public async Task PointAndTourQueriesLoadTheProviderNavigation()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var point = Assert.Single(await repository.SaveStampingPointsAsync(
            CreatePoint("Touringen", StampingProvider.TouringenId, "touringen-42", 42)));
        var tour = new HikingTour(7, "Test tour", null, null, null, false, false, false);
        context.HikingTours.Add(tour);
        context.StampingPointsInTours.Add(new SortedStampingPoint(1)
        {
            StampingPointId = point.Id,
            Tour = tour
        });
        await context.SaveChangesAsync();

        var pointResult = Assert.Single(await repository.GetStampingPointsAsync(providerFilter: StampingProviderFilter.Single(StampingProvider.TouringenId)));
        var tourResult = Assert.Single(await repository.GetHikingToursAsync());

        Assert.Equal(StampingProvider.TouringenSlug, pointResult.Point.Provider.Slug);
        Assert.Equal(StampingProvider.TouringenSlug, Assert.Single(tourResult.Points).Provider.Slug);
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
    public async Task SavingPointsUsesSeriesAndNumberAsImportIdentity()
    {
        await using var context = await CreateContextAsync();
        context.StampingProviders.Add(CreateProvider(99, "other"));
        context.StampingSeries.Add(CreateSeries(30, 99, "standard"));
        await context.SaveChangesAsync();
        var repository = new TouredRepository(context);

        var savedPoints = await repository.SaveStampingPointsAsync(
            CreatePoint("Touringen", StampingProvider.TouringenId, "shared", 42),
            CreatePoint("Natural treasure", StampingProvider.TouringenId, "natural-42", 42) with
            {
                SeriesId = StampingSeries.TouringenNaturalTreasuresId
            },
            CreatePoint("Other", 99, "shared", 42));

        Assert.Equal(3, savedPoints.Count);
        Assert.All(savedPoints, point => Assert.True(point.Id > 0));
        Assert.NotEqual(savedPoints[0].Id, savedPoints[1].Id);

        var updatedPoint = CreatePoint("Touringen updated", StampingProvider.TouringenId, "updated", 42) with { Id = 99_999 };
        var updated = Assert.Single(await repository.SaveStampingPointsAsync(updatedPoint));

        Assert.Equal(savedPoints[0].Id, updated.Id);
        Assert.Equal(3, await context.StampingPoints.CountAsync(p => p.ProviderId == StampingProvider.TouringenId || p.ProviderId == 99));
        Assert.Equal("Touringen updated", (await context.StampingPoints.SingleAsync(p => p.Id == updated.Id)).Name);
        Assert.Equal("updated", (await context.StampingPoints.SingleAsync(p => p.Id == updated.Id)).ExternalId);

        var temporary = new StampingPoint(default, "Temporary special", 11.1m, 50.1m, null, 0, StampingProvider.TouringenId, "special-campaign-2026")
        {
            SeriesId = StampingSeries.TouringenSpecialStampsId,
            ValidFrom = new DateOnly(2026, 6, 1),
            ValidUntil = new DateOnly(2026, 10, 31)
        };
        var savedTemporary = Assert.Single(await repository.SaveStampingPointsAsync(temporary));
        var updatedTemporary = Assert.Single(await repository.SaveStampingPointsAsync(temporary with { Name = "Updated temporary special" }));
        Assert.Equal(savedTemporary.Id, updatedTemporary.Id);
        Assert.Null(updatedTemporary.Number);
        Assert.Equal(4, await context.StampingPoints.CountAsync(p => p.ProviderId == StampingProvider.TouringenId || p.ProviderId == 99));
    }

    [Fact]
    public async Task TouringenImportPreservesVisitsWhenCorrectingStandardPointsOneThroughEight()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);

        var oldPoints = Enumerable.Range(1, 8).Select(number =>
            CreatePoint($"Old Naturschatz {number}", StampingProvider.TouringenId, $"standard-{number}", number) with
            {
                SeriesId = StampingSeries.TouringenStandardId,
                Latitude = 50.0m + number,
                Longitude = 11.0m + number
            }).ToArray();
        var savedOld = await repository.SaveStampingPointsAsync(oldPoints);

        var user = new User { Email = "hiker@example.test", DefaultStampingProviderId = StampingProvider.TouringenId };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        foreach (var p in savedOld)
        {
            context.UserVisits.Add(new UserVisit
            {
                UserId = user.Id,
                StampingPointId = p.Id,
                Visited = new DateTime(2026, 8, 1),
                HasVisitedTime = false,
                EntryCreated = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var canonicalStandard = Enumerable.Range(1, 8).Select(number =>
            CreatePoint($"Canonical Standard {number}", StampingProvider.TouringenId, $"standard-{number}", number) with
            {
                SeriesId = StampingSeries.TouringenStandardId,
                Latitude = 51.0m + number,
                Longitude = 12.0m + number
            }).ToArray();
        var canonicalNaturschaetze = Enumerable.Range(1, 8).Select(number =>
            CreatePoint($"Naturschatz {number}", StampingProvider.TouringenId, $"naturschaetze-{number}", number) with
            {
                SeriesId = StampingSeries.TouringenNaturalTreasuresId,
                Latitude = 52.0m + number,
                Longitude = 13.0m + number
            }).ToArray();

        var reimported = await repository.SaveStampingPointsAsync(canonicalStandard.Concat(canonicalNaturschaetze).ToArray());

        Assert.Equal(16, reimported.Count);
        Assert.Equal(16, await context.StampingPoints.CountAsync(p => p.ProviderId == StampingProvider.TouringenId));

        for (var i = 0; i < 8; i++)
        {
            var oldId = savedOld[i].Id;
            var standardPoint = await context.StampingPoints.SingleAsync(p => p.Id == oldId);
            Assert.Equal($"Canonical Standard {i + 1}", standardPoint.Name);
            Assert.Equal(51.0m + (i + 1), standardPoint.Latitude);
            Assert.Equal(12.0m + (i + 1), standardPoint.Longitude);
            Assert.Equal(StampingSeries.TouringenStandardId, standardPoint.SeriesId);

            var visit = await context.UserVisits.SingleAsync(v => v.StampingPointId == oldId && v.UserId == user.Id);
            Assert.NotNull(visit.Visited);
        }

        var naturalPoints = await context.StampingPoints
            .Where(p => p.SeriesId == StampingSeries.TouringenNaturalTreasuresId)
            .ToListAsync();
        Assert.Equal(8, naturalPoints.Count);
        Assert.All(naturalPoints, p => Assert.DoesNotContain(p.Id, savedOld.Select(o => o.Id)));
    }

    [Fact]
    public async Task UserImportUsesUsersDefaultProvider()
    {
        await using var context = await CreateContextAsync();
        context.StampingProviders.Add(CreateProvider(99, "other"));
        context.StampingSeries.Add(CreateSeries(30, 99, "standard"));
        await context.SaveChangesAsync();

        var user = new User { Email = "user@example.test", DefaultStampingProviderId = 99 };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new TouredRepository(context);
        var points = await repository.SaveStampingPointsAsync(
            CreatePoint("Touringen", StampingProvider.TouringenId, "touringen-42", 42),
            CreatePoint("Other", 99, "other-42", 42));
        var otherPoint = points.Single(p => p.ProviderId == 99);
        var manager = CreateImportManager(context, repository, user, null);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("42;01.02.2026;12:30"));

        await manager.ImportUserDataAsync(stream);

        var visit = Assert.Single(await context.UserVisits.AsNoTracking().ToListAsync());
        Assert.Equal(otherPoint.Id, visit.StampingPointId);
        Assert.Equal(new DateTime(2026, 2, 1, 12, 30, 0), visit.Visited);
        Assert.True(visit.HasVisitedTime);
    }

    [Fact]
    public async Task HarzerWandernadelImportWithinUnitOfWorkPreservesPointIdsAndVisits()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var existingPoints = await repository.SaveStampingPointsAsync(
            CreatePoint("Existing 44", StampingProvider.HarzerWandernadelId, "HWN044", 44),
            CreatePoint("Existing 45", StampingProvider.HarzerWandernadelId, "HWN045", 45));
        var existing44 = existingPoints.Single(point => point.Number == 44);
        var existing45 = existingPoints.Single(point => point.Number == 45);
        var user = new User { Email = "hwn@example.test" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await repository.AddUserVisitAsync(user, existing45.Id, new DateTime(2026, 8, 30, 12, 0, 0), true);
        var importedPoints = Enumerable.Range(1, 222)
            .Select(number => CreatePoint(
                $"Imported {number}",
                StampingProvider.HarzerWandernadelId,
                $"osm-node-{number}",
                number))
            .ToArray();
        var manager = CreateImportManager(context, repository, null, null, importedPoints);

        using (var unitOfWork = new UnitOfWork(context))
        {
            await manager.ImportHarzerWandernadelDataAsync();
            await unitOfWork.CommitAsync();
        }

        var storedPoints = await context.StampingPoints.AsNoTracking()
            .Where(point => point.ProviderId == StampingProvider.HarzerWandernadelId)
            .OrderBy(point => point.Number)
            .ToArrayAsync();
        Assert.Equal(222, storedPoints.Length);
        Assert.Equal(existing44.Id, storedPoints.Single(point => point.Number == 44).Id);
        Assert.Equal("Imported 44", storedPoints.Single(point => point.Number == 44).Name);
        Assert.Equal(existing45.Id, storedPoints.Single(point => point.Number == 45).Id);
        Assert.Equal("Imported 45", storedPoints.Single(point => point.Number == 45).Name);
        Assert.Equal(existing45.Id, Assert.Single(await context.UserVisits.AsNoTracking().ToArrayAsync()).StampingPointId);
        var import = Assert.Single(await context.Imports.AsNoTracking().ToArrayAsync());
        Assert.Equal(222, import.StampingPointsCount);
        Assert.Equal(0, import.HikingToursCount);
        var provider = await context.StampingProviders.AsNoTracking()
            .SingleAsync(item => item.Id == StampingProvider.HarzerWandernadelId);
        Assert.True(provider.IsAnonymousAccessAllowed);
        Assert.Equal("44", provider.DataSourceRevision);
        Assert.Equal("© OpenStreetMap contributors", provider.DataSourceAttribution);
        Assert.NotNull(provider.DataImportedAt);
    }

    [Fact]
    public async Task IncompleteHarzerWandernadelSnapshotDoesNotPublishProviderData()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var existing = Assert.Single(await repository.SaveStampingPointsAsync(
            CreatePoint("Existing 45", StampingProvider.HarzerWandernadelId, "HWN045", 45)));
        var manager = CreateImportManager(
            context,
            repository,
            null,
            null,
            [CreatePoint("Incomplete 45", StampingProvider.HarzerWandernadelId, "osm-node-45", 45)]);

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.ImportHarzerWandernadelDataAsync());

        var stored = Assert.Single(await context.StampingPoints.AsNoTracking()
            .Where(point => point.ProviderId == StampingProvider.HarzerWandernadelId)
            .ToArrayAsync());
        var provider = await context.StampingProviders.AsNoTracking()
            .SingleAsync(item => item.Id == StampingProvider.HarzerWandernadelId);
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("Existing 45", stored.Name);
        Assert.False(provider.IsAnonymousAccessAllowed);
        Assert.Null(provider.DataImportedAt);
        Assert.Empty(await context.Imports.AsNoTracking().ToArrayAsync());
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

        var point = await context.StampingPoints.AsNoTracking().SingleAsync(p => p.ProviderId == StampingProvider.TouringenId);
        var relations = await context.StampingPointsInTours.AsNoTracking().ToListAsync();
        Assert.NotEqual(secondRawPoint.Id, point.Id);
        Assert.Equal(secondRawPoint.Id.ToString(), point.ExternalId);
        Assert.Equal(2, relations.Count);
        Assert.All(relations, relation => Assert.Equal(point.Id, relation.StampingPointId));
    }

    [Fact]
    public async Task TouringenImportPreservesExistingVisitsWhenUpdatingCanonicalStandardPoints()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var existing = Assert.Single(await repository.SaveStampingPointsAsync(
            CreatePoint("Urwaldpfad Leutenberg", StampingProvider.TouringenId, "976", 1)));
        var user = new User { Email = "visited@example.test" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await repository.AddUserVisitAsync(user, existing.Id, null, false);
        var canonicalRawPoint = CreateRawStampPoint(101, 1) with
        {
            Title = "Schleifkotengrund",
            Name = "Schleifkotengrund",
            Latitude = 50.84094m,
            Longitude = 10.38172m
        };
        var rawTour = new RawTour(101, "Tour", [canonicalRawPoint], false, true, false, null, "Start", "End");
        var rawData = JsonSerializer.Serialize(new[] { new RawArea(1, "Area", [rawTour], []) });
        var manager = CreateImportManager(context, repository, null, rawData);

        await manager.ImportTouringenDataAsync();

        var updated = await context.StampingPoints.AsNoTracking().SingleAsync(p => p.ProviderId == StampingProvider.TouringenId);
        Assert.Equal(existing.Id, updated.Id);
        Assert.Equal("Schleifkotengrund", updated.Name);
        Assert.Equal(existing.Id, (await context.UserVisits.AsNoTracking().SingleAsync()).StampingPointId);
    }

    [Fact]
    public async Task TouringenImportSavesOsmProvenanceAndEnablesGeoJsonExport()
    {
        await using var context = await CreateContextAsync();
        var repository = new TouredRepository(context);
        var canonicalRawPoint = CreateRawStampPoint(101, 1);
        var rawTour = new RawTour(101, "Tour", [canonicalRawPoint], false, true, false, null, "Start", "End");
        var rawData = JsonSerializer.Serialize(new[] { new RawArea(1, "Area", [rawTour], []) });
        var manager = CreateImportManager(context, repository, null, rawData);

        await manager.ImportTouringenDataAsync();

        var provider = await context.StampingProviders.AsNoTracking().SingleAsync(p => p.Id == StampingProvider.TouringenId);
        Assert.Equal("© OpenStreetMap contributors", provider.DataSourceAttribution);
        Assert.Equal("Open Data Commons Open Database License (ODbL) 1.0", provider.DataLicenseName);
        Assert.Equal("https://opendatacommons.org/licenses/odbl/1-0/", provider.DataLicenseUri?.AbsoluteUri);
        Assert.Equal("https://www.openstreetmap.org/relation/14773147", provider.DataSourceUri?.AbsoluteUri);
        Assert.Equal("45", provider.DataSourceRevision);
        Assert.NotNull(provider.DataImportedAt);
        Assert.True(provider.IsAnonymousAccessAllowed);

        var publicData = await repository.GetPublicProviderDataAsync(StampingProvider.TouringenSlug);
        Assert.NotNull(publicData);
        Assert.Single(publicData.Value.Points);
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

    private static ImportManager CreateImportManager(
        DataContext context,
        TouredRepository repository,
        User? user,
        string? rawData,
        IReadOnlyList<StampingPoint>? harzerWandernadelPoints = null)
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
            new StubHarzerWandernadelImportService(harzerWandernadelPoints ?? []),
            new StubTouringenStampingPointImportService(CreateTouringenPoints(rawData)),
            Options.Create(new TouringenWebsiteConfiguration { StempelstellenUri = new Uri("https://example.test/stamping-points") }),
            new HikingToursImportService(),
            repository);
    }

    private static IReadOnlyList<StampingPoint> CreateTouringenPoints(string? rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData)) return [];
        var areas = JsonSerializer.Deserialize<RawArea[]>(rawData) ?? [];
        return areas.SelectMany(area => area.Touren.SelectMany(tour => tour.StampPoints))
            .Union(areas.SelectMany(area => area.OrphanedStampPoints))
            .DistinctBy(point => point.Id)
            .GroupBy(point => point.StampPointNumber)
            .Select(group => group.MaxBy(point => point.Id)!.CreateStampingPoint())
            .ToArray();
    }

    private static RawStampPoint CreateRawStampPoint(int externalId, int number)
        => new(externalId, $"Point {number}", 50.0m, 11.0m, 1, number, number * 10, $"Point {number}");

    private static StampingProvider CreateProvider(int id, string slug)
        => new() { Id = id, Slug = slug, Name = slug };

    private static StampingSeries CreateSeries(int id, int providerId, string slug)
        => new() { Id = id, ProviderId = providerId, Slug = slug, Name = slug };

    private static StampingPoint CreatePoint(string name, int providerId, string externalId, int number)
        => new(default, name, 11.0m, 50.0m, number, number * 10, providerId, externalId)
        {
            SeriesId = providerId switch
            {
                StampingProvider.TouringenId => StampingSeries.TouringenStandardId,
                StampingProvider.HarzerWandernadelId => StampingSeries.HarzerWandernadelStandardId,
                StampingProvider.MalerwegId => StampingSeries.MalerwegStandardId,
                _ => 30
            }
        };

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

    private sealed class StubHarzerWandernadelImportService(IReadOnlyList<StampingPoint> points) : IHarzerWandernadelImportService
    {
        public Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new StampingPointSourceSnapshot(
                points,
                new Uri("https://www.openstreetmap.org/relation/148007"),
                "© OpenStreetMap contributors",
                "Open Data Commons Open Database License (ODbL) 1.0",
                new Uri("https://opendatacommons.org/licenses/odbl/1-0/"),
                "44",
                new DateTime(2026, 3, 9, 22, 17, 30, DateTimeKind.Utc)));
    }

    private sealed class StubTouringenStampingPointImportService(IReadOnlyList<StampingPoint> points) : ITouringenStampingPointImportService
    {
        public Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new StampingPointSourceSnapshot(
                points,
                new Uri("https://www.openstreetmap.org/relation/14773147"),
                "© OpenStreetMap contributors",
                "Open Data Commons Open Database License (ODbL) 1.0",
                new Uri("https://opendatacommons.org/licenses/odbl/1-0/"),
                "45",
                new DateTime(2026, 8, 31, 15, 14, 0, DateTimeKind.Utc)));
    }
}
