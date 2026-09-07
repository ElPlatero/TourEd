using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Controllers.Admin;
using Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class ImportTransactionIntegrationTests : IAsyncLifetime
{
    private const string Email = "import-transactions@example.test";
    private const string Token = "test-only-import-transaction-token-0123456789abcdef";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-import-transactions-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-import-keys-{Guid.NewGuid():N}");
    private ImportFactory _factory = null!;
    private int _userId;
    private int _visitPointId;

    public async Task InitializeAsync()
    {
        _factory = new ImportFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.Database.MigrateAsync();
        var user = new User { Email = Email, DefaultStampingProviderId = StampingProvider.MalerwegId };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;
        db.UserStampingProviders.Add(new UserStampingProvider { UserId = user.Id, StampingProviderId = StampingProvider.MalerwegId });
        await db.SaveChangesAsync();
        _visitPointId = await db.StampingPoints.Where(p => p.ProviderId == StampingProvider.MalerwegId && p.Number == 1).Select(p => p.Id).SingleAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        if (Directory.Exists(_keysPath)) Directory.Delete(_keysPath, true);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("touringen")]
    [InlineData("harzer-wandernadel")]
    public async Task PausedDownloadDoesNotBlockIndependentVisitWrite(string provider)
    {
        _factory.Sources.HoldDownload = true;
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var import = client.PostAsync($"/api/admin/imports/{provider}", null);
        try
        {
            await _factory.Sources.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await WriteIndependentVisitAsync();
            Assert.Equal(0, _factory.TransactionsStarted);
        }
        finally
        {
            _factory.Sources.Release.TrySetResult();
            using var response = await import;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(1, _factory.TransactionsStarted);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Single(await db.UserVisits.Where(v => v.UserId == _userId && v.StampingPointId == _visitPointId).ToListAsync());
        Assert.Single(await db.Imports.ToListAsync());
    }

    [Fact]
    public async Task PausedCsvReadDoesNotBlockIndependentVisitWrite()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(Constants.ClaimsNames.UserId, _userId.ToString()),
                new Claim(Constants.ClaimsNames.UserEmail, Email)
            }, "test"))
        };
        var controller = new ImportsController(scope.ServiceProvider.GetRequiredService<IImportManager>());
        await using var stream = new PausedStream(Encoding.UTF8.GetBytes("2;01.02.2026;12:30"));
        // MVC normally buffers multipart uploads; pause the parser's file stream here.
        var import = controller.CreateNewUserDataImport(new FormFileCollection
        {
            new FormFile(stream, 0, stream.Length, "csvImport", "visits.csv")
        });
        try
        {
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await WriteIndependentVisitAsync();
            Assert.Equal(0, _factory.TransactionsStarted);
        }
        finally
        {
            stream.Release.TrySetResult();
            await import;
        }
        Assert.Equal(1, _factory.TransactionsStarted);
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        Assert.Equal(2, await verifyScope.ServiceProvider.GetRequiredService<DataContext>().UserVisits.CountAsync(v => v.UserId == _userId));
    }

    [Theory]
    [InlineData("touringen")]
    [InlineData("harzer-wandernadel")]
    public async Task FinalSaveFailureRollsBackPointsRelationshipsMetadataAndImport(string providerSlug)
    {
        var providerId = providerSlug == "touringen" ? StampingProvider.TouringenId : StampingProvider.HarzerWandernadelId;
        int originalId;
        bool originalReadiness;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var repository = scope.ServiceProvider.GetRequiredService<TouredRepository>();
            var point = Assert.Single(await repository.SaveStampingPointsAsync(Sources.Point(providerId, 1) with { Name = "Original" }));
            originalId = point.Id;
            await repository.AddUserVisitAsync(new User { Id = _userId }, point.Id, null, false);
            originalReadiness = (await db.StampingProviders.SingleAsync(p => p.Id == providerId)).IsAnonymousAccessAllowed;
            var failureTrigger = providerSlug == "touringen"
                ? "CREATE TRIGGER fail_final_import BEFORE INSERT ON SortedStampingPoint BEGIN SELECT RAISE(ABORT, 'Injected import failure'); END;"
                : "CREATE TRIGGER fail_final_import BEFORE INSERT ON Imports BEGIN SELECT RAISE(ABORT, 'Injected import failure'); END;";
            await db.Database.ExecuteSqlRawAsync(failureTrigger);
            var manager = scope.ServiceProvider.GetRequiredService<IImportManager>();
            await Assert.ThrowsAsync<DbUpdateException>(() => providerSlug == "touringen"
                ? manager.ImportTouringenDataAsync() : manager.ImportHarzerWandernadelDataAsync());
        }
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var savedPoint = Assert.Single(await verify.StampingPoints.Where(p => p.ProviderId == providerId).ToListAsync());
        Assert.Equal(originalId, savedPoint.Id);
        Assert.Equal("Original", savedPoint.Name);
        Assert.Single(await verify.UserVisits.Where(v => v.StampingPointId == originalId).ToListAsync());
        Assert.Empty(await verify.HikingTours.ToListAsync());
        Assert.Empty(await verify.StampingPointsInTours.ToListAsync());
        Assert.Empty(await verify.Imports.ToListAsync());
        var provider = await verify.StampingProviders.SingleAsync(p => p.Id == providerId);
        Assert.Equal(originalReadiness, provider.IsAnonymousAccessAllowed);
        Assert.Null(provider.DataSourceRevision);
        Assert.Null(provider.DataImportedAt);
        Assert.Null(provider.DataSourceUri);
        Assert.Null(provider.DataSourceAttribution);
        Assert.Null(provider.DataLicenseUri);
    }

    [Theory]
    [InlineData("touringen")]
    [InlineData("harzer-wandernadel")]
    public async Task InvalidSourceDataIsRejectedBeforeStartingTransaction(string provider)
    {
        _factory.Sources.Invalid = true;
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IImportManager>();
        await Assert.ThrowsAsync<InvalidDataException>(() => provider == "touringen"
            ? manager.ImportTouringenDataAsync() : manager.ImportHarzerWandernadelDataAsync());
        Assert.Equal(0, _factory.TransactionsStarted);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DataContext>().Imports.ToListAsync());
    }

    [Fact]
    public async Task InvalidCsvDateIsRejectedBeforeStartingTransaction()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(Constants.ClaimsNames.UserId, _userId.ToString()),
                new Claim(Constants.ClaimsNames.UserEmail, Email)
            }, "test"))
        };
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("2;31.02.2026;12:30"));
        var manager = scope.ServiceProvider.GetRequiredService<IImportManager>();
        var result = await manager.ImportUserDataAsync(stream);
        Assert.Equal(1, Assert.Single(result.Errors).Line);
        Assert.Equal(0, _factory.TransactionsStarted);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DataContext>().UserVisits.ToListAsync());
    }

    [Fact]
    public async Task CsvPreservesOptionalDatesAndReportsExistingWithoutOverwriting()
    {
        using var response = await UploadCsv("1;;\n2;04.09.2026;\n3;04.09.2026;00:00\n4;04.09.2026;12:34\n");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<UserDataImportResult>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new[] { 4, 0, 0 }, new[] { result.Imported, result.Existing, result.Rejected });
        using var repeat = await UploadCsv("1;01.01.2020;01:23\n2;;\n3;;\n4;;\n5;;");
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var repeated = JsonSerializer.Deserialize<UserDataImportResult>(await repeat.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new[] { 1, 4, 0 }, new[] { repeated.Imported, repeated.Existing, repeated.Rejected });
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var visits = await db.UserVisits.Join(db.StampingPoints, v => v.StampingPointId, p => p.Id,
            (v, p) => new { p.Number, v.Visited, v.HasVisitedTime, v.UserId, p.ProviderId }).OrderBy(v => v.Number).ToListAsync();
        Assert.Equal(5, visits.Count);
        Assert.All(visits, v => { Assert.Equal(_userId, v.UserId); Assert.Equal(StampingProvider.MalerwegId, v.ProviderId); });
        Assert.Null(visits[0].Visited);
        Assert.False(visits[0].HasVisitedTime);
        Assert.Equal(new DateTime(2026, 9, 4), visits[1].Visited);
        Assert.False(visits[1].HasVisitedTime);
        Assert.Equal(new DateTime(2026, 9, 4), visits[2].Visited);
        Assert.True(visits[2].HasVisitedTime);
        Assert.Equal(new DateTime(2026, 9, 4, 12, 34, 0), visits[3].Visited);
        Assert.True(visits[3].HasVisitedTime);
    }

    [Theory]
    [InlineData("2;;12:30")]
    [InlineData("2;31.02.2026;")]
    [InlineData("2;04.09.2026;24:00")]
    [InlineData("2;04.09.2026;12:60")]
    [InlineData("1001;;")]
    [InlineData("prefix2;;")]
    [InlineData("2;;suffix")]
    [InlineData("2;04.09.2026;12:30junk")]
    [InlineData("2;;;")]
    [InlineData("2;")]
    [InlineData("0;;")]
    [InlineData("1;;")]
    [InlineData("001;;")]
    [InlineData("999;;")]
    [InlineData("")]
    public async Task CsvRejectsWholeFileWithLineNumber(string invalidLine)
    {
        using var response = await UploadCsv("1;;\n" + invalidLine + "\n3;;");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = JsonSerializer.Deserialize<UserDataImportResult>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, Assert.Single(result.Errors).Line);
        Assert.Equal(1, result.Rejected);
        Assert.Equal(0, result.Imported);
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DataContext>().UserVisits.ToListAsync());
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "")]
    [InlineData(2, "1;;")]
    public async Task CsvRequiresExactlyOneNonEmptyFile(int fileCount, string csv)
    {
        using var response = await UploadCsv(csv, fileCount);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.TransactionsStarted);
    }

    private async Task<HttpResponseMessage> UploadCsv(string csv, int fileCount = 1)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var form = new MultipartFormDataContent();
        for (var i = 0; i < fileCount; i++)
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "csvImport", $"visits{i}.csv");
        return await client.PostAsync("/api/admin/imports", form);
    }

    private async Task WriteIndependentVisitAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<TouredRepository>();
        await repository.AddUserVisitAsync(new User { Id = _userId }, _visitPointId, null, false);
    }

    private sealed class ImportFactory(string databasePath, string keysPath) : WebApplicationFactory<Program>
    {
        public Sources Sources { get; } = new();
        public int TransactionsStarted;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={databasePath};Default Timeout=1",
                ["Authentication:Google:ClientId"] = "test-client-id",
                ["Authentication:Google:ClientSecret"] = "test-client-secret",
                ["Authentication:Cli:UserEmail"] = Email,
                ["Authentication:Cli:Token"] = Token,
                ["DataProtection:KeysPath"] = keysPath,
                ["touringen:StempelstellenUri"] = "https://example.test/stamps"
            }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHarzerWandernadelImportService>(Sources);
                services.AddSingleton<ITouringenStampingPointImportService>(Sources);
                services.AddSingleton<IHtmlParsingService>(Sources);
                services.AddTransient<Func<IUnitOfWork>>(provider => () =>
                {
                    Interlocked.Increment(ref TransactionsStarted);
                    return provider.GetRequiredService<IUnitOfWork>();
                });
            });
        }
    }

    private sealed class Sources : IHarzerWandernadelImportService, ITouringenStampingPointImportService, IHtmlParsingService
    {
        public bool HoldDownload;
        public bool Invalid;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> GetRawDmoStringAsync(Uri uri)
        {
            var point = new RawStampPoint(9001, "Point", 50m, 11m, 1, Invalid ? 2 : 1, 10, "Point");
            var tour = new RawTour(901, "Imported tour", [point], false, true, false, null, "Start", "End");
            return Task.FromResult<string?>(JsonSerializer.Serialize(new[] { new RawArea(1, "Area", [tour], []) }));
        }
        Task<StampingPointSourceSnapshot> IHarzerWandernadelImportService.DownloadStampingPointsAsync(CancellationToken cancellationToken)
            => Download(StampingProvider.HarzerWandernadelId, Invalid ? 221 : 222, cancellationToken);
        Task<StampingPointSourceSnapshot> ITouringenStampingPointImportService.DownloadStampingPointsAsync(CancellationToken cancellationToken)
            => Download(StampingProvider.TouringenId, 1, cancellationToken);
        private async Task<StampingPointSourceSnapshot> Download(int providerId, int count, CancellationToken cancellationToken)
        {
            if (HoldDownload)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return new StampingPointSourceSnapshot(Enumerable.Range(1, count).Select(n => Point(providerId, n)).ToArray(),
                new Uri("https://example.test/source"), "Test attribution", "Test licence", new Uri("https://example.test/licence"),
                "new-revision", DateTime.UtcNow);
        }
        public static StampingPoint Point(int providerId, int number) => new(default, $"Imported {number}", 11m, 50m, number, number, providerId, $"source-{number}")
        {
            SeriesId = providerId == StampingProvider.TouringenId ? StampingSeries.TouringenStandardId : StampingSeries.HarzerWandernadelStandardId
        };
    }

    private sealed class PausedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
