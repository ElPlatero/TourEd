using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Authentication;
using Api.Controllers.Auth;
using Api.Controllers.Points;
using Api.Controllers.Providers;
using Api.Controllers.Tours;
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
using TourEd.Lib.Abstractions;
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
    private const int RestrictedProviderPointNumber = 45;
    private const int OtherProviderId = 10;
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
            Id = OtherProviderId,
            Slug = "other",
            Name = "Other provider",
            IsAnonymousAccessAllowed = true,
            Description = "Other provider description.",
            WebsiteUri = new Uri("https://provider.example.test/info")
        });
        context.StampingProviders.Add(new StampingProvider
        {
            Id = 11,
            Slug = "unsupported-link",
            Name = "Unsupported link provider",
            IsAnonymousAccessAllowed = true,
            Description = "Provider with a non-public website scheme.",
            WebsiteUri = new Uri("ftp://provider.example.test/info")
        });
        context.StampingSeries.Add(new StampingSeries
        {
            Id = 30,
            ProviderId = OtherProviderId,
            Slug = "standard",
            Name = "Standard"
        });
        var user = new User
        {
            Email = FakeGoogleHandler.Email,
            DefaultStampingProviderId = StampingProvider.TouringenId
        };
        var visitedPoint = CreatePoint(VisitedPointNumber, StampingProvider.TouringenId);
        context.Users.Add(user);
        context.StampingPoints.AddRange(
            visitedPoint,
            CreatePoint(UnvisitedPointNumber, StampingProvider.TouringenId),
            CreatePoint(WritablePointNumber, StampingProvider.TouringenId),
            CreatePoint(OtherProviderPointNumber, OtherProviderId),
            CreatePoint(RestrictedProviderPointNumber, StampingProvider.HarzerWandernadelId));
        await context.SaveChangesAsync();
        context.UserStampingProviders.AddRange(await context.StampingProviders
            .Select(provider => new UserStampingProvider
            {
                UserId = user.Id,
                StampingProviderId = provider.Id
            })
            .ToArrayAsync());
        context.UserVisits.Add(new UserVisit
        {
            UserId = user.Id,
            StampingPointId = visitedPoint.Id,
            Visited = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc),
            HasVisitedTime = true
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
        Assert.Equal(3, properties.Count);
        Assert.True(sessionDocument.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.Equal(FakeGoogleHandler.Email, sessionDocument.RootElement.GetProperty("email").GetString());
        Assert.True(sessionDocument.RootElement.TryGetProperty("expiresAt", out var expiresAtProperty));
        Assert.True(expiresAtProperty.TryGetDateTimeOffset(out var expiresAt));
        Assert.True(expiresAt > DateTimeOffset.UtcNow);
        Assert.DoesNotContain("token", sessionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subject", sessionJson, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/?provider=touringen&point=123", "/?provider=touringen&point=123")]
    [InlineData("https://attacker.example/", "/")]
    [InlineData("//attacker.example/", "/")]
    public async Task GoogleLoginPreservesOnlyLocalReturnUrls(string returnUrl, string expectedRedirect)
    {
        using var client = CreateClient(_factory);
        var loginUrl = QueryHelpers.AddQueryString("/auth/login", "returnUrl", returnUrl);

        var challengeResponse = await client.GetAsync(loginUrl);
        Assert.Equal(HttpStatusCode.Redirect, challengeResponse.StatusCode);

        var callbackResponse = await client.GetAsync(challengeResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal(expectedRedirect, callbackResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnknownGoogleUserCreatesRegistrationRequestAndRedirectsToPendingRegistration()
    {
        using var client = CreateClient(_factory);

        var callbackResponse = await client.GetAsync("/fake-google/callback?email=newhiker@example.test&subject=google-sub-new");

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal("/?registration=pending", callbackResponse.Headers.Location?.OriginalString);

        var session = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
        Assert.NotNull(session);
        Assert.False(session.Authenticated);
        Assert.Null(session.Email);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var request = await context.RegistrationRequests.SingleOrDefaultAsync(r => r.GoogleSubject == "google-sub-new");
        Assert.NotNull(request);
        Assert.Equal("newhiker@example.test", request.Email);
        Assert.Equal(RegistrationRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task RealGoogleHandlerRedirectsPendingRegistrationWithoutCreatingSessionCookie()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"toured-google-callback-{Guid.NewGuid():N}.db");
        var keysPath = Path.Combine(Path.GetTempPath(), $"toured-google-callback-keys-{Guid.NewGuid():N}");
        await using var factory = new TouredWebApplicationFactory(
            databasePath,
            keysPath,
            useFakeGoogle: false,
            useStubGoogleBackchannel: true);

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<DataContext>().Database.MigrateAsync();
            }

            using var client = CreateClient(factory);
            var challengeResponse = await client.GetAsync("/auth/login");
            Assert.Equal(HttpStatusCode.Redirect, challengeResponse.StatusCode);
            var state = QueryHelpers.ParseQuery(challengeResponse.Headers.Location!.Query)["state"].ToString();

            var callbackResponse = await client.GetAsync($"/signin-google?code=test-code&state={Uri.EscapeDataString(state)}");

            Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
            Assert.Equal("/?registration=pending", callbackResponse.Headers.Location?.OriginalString);
            Assert.DoesNotContain(
                callbackResponse.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
                cookie => cookie.StartsWith("toured-session=", StringComparison.Ordinal));

            var session = await client.GetFromJsonAsync<AuthSessionResponse>("/auth/session");
            Assert.NotNull(session);
            Assert.False(session.Authenticated);

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var context = verificationScope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.NotNull(await context.RegistrationRequests.SingleOrDefaultAsync(
                request => request.GoogleSubject == "google-sub-real-handler"));
        }
        finally
        {
            DeleteFile(databasePath);
            DeleteDirectory(keysPath);
        }
    }

    [Fact]
    public async Task RealGoogleHandlerPreservesRejectedRegistrationAndShowsRejectedOutcome()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"toured-google-rejected-{Guid.NewGuid():N}.db");
        var keysPath = Path.Combine(Path.GetTempPath(), $"toured-google-rejected-keys-{Guid.NewGuid():N}");
        await using var factory = new TouredWebApplicationFactory(
            databasePath,
            keysPath,
            useFakeGoogle: false,
            useStubGoogleBackchannel: true);
        var decidedAt = DateTime.UtcNow.AddDays(-2);

        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                await context.Database.MigrateAsync();
                context.RegistrationRequests.Add(new RegistrationRequest
                {
                    GoogleSubject = "google-sub-real-handler",
                    Email = "real-handler@example.test",
                    Status = RegistrationRequestStatus.Rejected,
                    CreatedAt = decidedAt.AddDays(-1),
                    UpdatedAt = decidedAt,
                    DecidedAt = decidedAt
                });
                await context.SaveChangesAsync();
            }

            using var client = CreateClient(factory);
            var challengeResponse = await client.GetAsync("/auth/login");
            var state = QueryHelpers.ParseQuery(challengeResponse.Headers.Location!.Query)["state"].ToString();
            var callbackResponse = await client.GetAsync($"/signin-google?code=test-code&state={Uri.EscapeDataString(state)}");

            Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
            Assert.Equal("/?registration=rejected", callbackResponse.Headers.Location?.OriginalString);
            Assert.DoesNotContain(
                callbackResponse.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
                cookie => cookie.StartsWith("toured-session=", StringComparison.Ordinal));

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<DataContext>();
            var request = await verificationContext.RegistrationRequests.SingleAsync();
            Assert.Equal(RegistrationRequestStatus.Rejected, request.Status);
            Assert.Equal(decidedAt, request.DecidedAt);
        }
        finally
        {
            DeleteFile(databasePath);
            DeleteDirectory(keysPath);
        }
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
            new SaveVisitRequest(null, null));

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
            new SaveVisitRequest(null, null));

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
        Assert.Contains(visitedPoints, point => point.Number == VisitedPointNumber && point.IsVisited && point.VisitedAt is not null);
        Assert.DoesNotContain(unvisitedPoints, point => point.Number == VisitedPointNumber);
        Assert.Contains(unvisitedPoints, point => point.Number == UnvisitedPointNumber && !point.IsVisited);
        Assert.DoesNotContain(visitedPoints.Concat(unvisitedPoints), point => point.Number == OtherProviderPointNumber);
        Assert.All(visitedPoints.Concat(unvisitedPoints), point => Assert.Equal(StampingProvider.TouringenSlug, point.Provider.Slug));
    }

    [Fact]
    public async Task AnonymousRequestsToPointsEndpointsAreRejectedWithUnauthorized()
    {
        using var client = CreateClient(_factory);

        var anonymousResponse = await client.GetAsync("/api/points");
        var visitedResponse = await client.GetAsync("/api/points?vis=true");
        var unvisitedResponse = await client.GetAsync("/api/points?vis=false");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visitedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unvisitedResponse.StatusCode);
        Assert.Null(visitedResponse.Headers.Location);
        Assert.Null(unvisitedResponse.Headers.Location);
    }

    [Fact]
    public async Task AllProviderPointQueriesRequireAuthenticationAndReturnCorrectPoints()
    {
        using var anonymousClient = CreateClient(_factory);
        var anonymousResponse = await anonymousClient.GetAsync("/api/points?provider=all");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var authenticatedClient = CreateClient(_factory);
        await LoginAsync(authenticatedClient);
        var visitedResponse = await authenticatedClient.GetFromJsonAsync<GetStampingPointsResponse>(
            "/api/points?provider=all&vis=true");
        var unvisitedResponse = await authenticatedClient.GetFromJsonAsync<GetStampingPointsResponse>(
            "/api/points?provider=all&vis=false");

        Assert.NotNull(visitedResponse);
        Assert.NotNull(unvisitedResponse);
        Assert.Contains(visitedResponse.StampingPoints, point =>
            point.Number == VisitedPointNumber && point.IsVisited && point.VisitedAt is not null);
        Assert.Contains(unvisitedResponse.StampingPoints, point =>
            point.Provider.Slug == "other" && point.Number == OtherProviderPointNumber && !point.IsVisited);
        Assert.Contains(unvisitedResponse.StampingPoints, point =>
            point.Provider.Slug == StampingProvider.HarzerWandernadelSlug &&
            point.Provider.Abbreviation == "HWN" &&
            point.Number == RestrictedProviderPointNumber && !point.IsVisited);
        Assert.DoesNotContain(unvisitedResponse.StampingPoints, point => point.Number == VisitedPointNumber);
    }

    [Fact]
    public async Task ProviderCatalogRequiresAuthenticationAndExposesConfiguredWebsiteUrls()
    {
        using var client = CreateClient(_factory);

        var anonymousResponse = await client.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        await LoginAsync(client);
        var authenticatedResponse = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");

        Assert.NotNull(authenticatedResponse);
        Assert.Equal(10, authenticatedResponse.OverallCount);
        var providers = authenticatedResponse.StampingProviders.ToArray();
        Assert.Equal([
            "Bliessteig",
            "Harzer Klosterwanderweg",
            "Harzer Wandernadel",
            "Heidschnuckenweg",
            "Kellerwaldsteig",
            "Malerweg",
            "Other provider",
            "Schluchtensteig",
            "Touringen",
            "Unsupported link provider"
        ], providers.Select(p => p.Name));

        var touringen = providers.Single(p => p.Slug == StampingProvider.TouringenSlug);
        Assert.True(touringen.IsAnonymousAccessAllowed);
        Assert.Contains("430 offizielle Stempelstellen", touringen.Description, StringComparison.Ordinal);
        Assert.Equal("https://www.touringen.de/", touringen.WebsiteUrl);

        var other = providers.Single(p => p.Slug == "other");
        Assert.Equal("https://provider.example.test/info", other.WebsiteUrl);
        var bliessteig = providers.Single(p => p.Slug == StampingProvider.BliessteigSlug);
        Assert.Equal("BS", bliessteig.Abbreviation);
        Assert.Equal("Creative Commons Namensnennung 4.0 International (CC BY 4.0)", bliessteig.DataLicenseName);
        Assert.False(bliessteig.HasPublicDataDownload);
        var kellerwaldsteig = providers.Single(p => p.Slug == StampingProvider.KellerwaldsteigSlug);
        Assert.Equal("KWS", kellerwaldsteig.Abbreviation);
        Assert.Contains("CC BY-SA 4.0", kellerwaldsteig.DataLicenseName, StringComparison.Ordinal);
        Assert.False(kellerwaldsteig.HasPublicDataDownload);
        var unsupported = providers.Single(p => p.Slug == "unsupported-link");
        Assert.Null(unsupported.WebsiteUrl);

        var harzerWandernadel = Assert.Single(authenticatedResponse.StampingProviders,
            provider => provider.Slug == StampingProvider.HarzerWandernadelSlug);
        Assert.Equal("HWN", harzerWandernadel.Abbreviation);
        Assert.False(harzerWandernadel.IsAnonymousAccessAllowed);
        Assert.Equal("https://www.harzer-wandernadel.de/", harzerWandernadel.WebsiteUrl);
    }

    [Fact]
    public async Task ProviderEntitlementsRestrictEveryReadAndWritePathWithoutDeletingVisits()
    {
        int userId;
        int otherPointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var user = await context.Users.SingleAsync(item => item.Email == FakeGoogleHandler.Email);
            var otherProvider = await context.StampingProviders.SingleAsync(item => item.Id == OtherProviderId);
            var otherPoint = await context.StampingPoints.SingleAsync(item =>
                item.ProviderId == OtherProviderId && item.Number == OtherProviderPointNumber);
            var touringenPoint = await context.StampingPoints.SingleAsync(item =>
                item.ProviderId == StampingProvider.TouringenId && item.Number == UnvisitedPointNumber);
            userId = user.Id;
            otherPointId = otherPoint.Id;

            otherProvider.DataSourceUri = new Uri("https://provider.example.test/source");
            otherProvider.DataSourceAttribution = "Provider test data";
            otherProvider.DataLicenseName = "Test licence";
            otherProvider.DataLicenseUri = new Uri("https://provider.example.test/licence");
            otherProvider.DataSourceRevision = "1";
            otherProvider.DataSourceUpdatedAt = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
            otherProvider.DataImportedAt = new DateTime(2026, 8, 31, 11, 0, 0, DateTimeKind.Utc);

            context.UserVisits.Add(new UserVisit
            {
                UserId = user.Id,
                StampingPointId = otherPoint.Id
            });
            var tour = new HikingTour(999, "Entitlement test tour", null, null, null, false, false, false);
            context.HikingTours.Add(tour);
            context.StampingPointsInTours.AddRange(
                new SortedStampingPoint(1) { StampingPointId = touringenPoint.Id, Tour = tour },
                new SortedStampingPoint(2) { StampingPointId = otherPoint.Id, Tour = tour });
            var access = await context.UserStampingProviders.SingleAsync(item =>
                item.UserId == user.Id && item.StampingProviderId == OtherProviderId);
            context.UserStampingProviders.Remove(access);
            await context.SaveChangesAsync();
        }

        using var client = CreateClient(_factory);
        await LoginAsync(client);

        var catalog = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        var allPoints = await client.GetFromJsonAsync<GetStampingPointsResponse>("/api/points?provider=all&vis=true");
        var directPoint = await client.GetAsync($"/api/points/{OtherProviderPointNumber}?provider=other");
        var directWrite = await client.PutAsJsonAsync(
            $"/api/points/{OtherProviderPointNumber}?provider=other",
            new SaveVisitRequest(null, null));
        var directStateWrite = await client.PutAsJsonAsync(
            $"/api/points/id/{otherPointId}/state?provider=other",
            new SynchronizeVisitRequest(
                new VisitStateRequest(true, null, null),
                new VisitStateRequest(false, null, null)));
        var geoJson = await client.GetAsync("/api/providers/other/points.geojson");
        var tours = await client.GetFromJsonAsync<GetHikingToursResponse>("/api/tours");

        Assert.NotNull(catalog);
        var otherCatalogProvider = Assert.Single(catalog.StampingProviders, provider => provider.Slug == "other");
        Assert.False(otherCatalogProvider.IsEnabled);
        Assert.True(otherCatalogProvider.IsDataReady);
        Assert.Equal(1, otherCatalogProvider.TotalPoints);
        Assert.Equal(1, otherCatalogProvider.VisitedPoints);
        Assert.False(otherCatalogProvider.HasPublicDataDownload);
        Assert.NotNull(allPoints);
        Assert.DoesNotContain(allPoints.StampingPoints, point => point.Provider.Slug == "other");
        Assert.Equal(HttpStatusCode.Forbidden, directPoint.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, directWrite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, directStateWrite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, geoJson.StatusCode);
        Assert.NotNull(tours);
        var entitlementTour = Assert.Single(tours.HikingTours, tour => tour.Id == 999);
        Assert.DoesNotContain(entitlementTour.StampingPoints!, point => point.Provider.Slug == "other");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.True(await context.UserVisits.AnyAsync(visit =>
                visit.UserId == userId && visit.StampingPointId == otherPointId));
            context.UserStampingProviders.Add(new UserStampingProvider
            {
                UserId = userId,
                StampingProviderId = OtherProviderId
            });
            await context.SaveChangesAsync();
        }

        var restoredPoint = await client.GetAsync($"/api/points/{OtherProviderPointNumber}?provider=other");
        Assert.Equal(HttpStatusCode.OK, restoredPoint.StatusCode);
        Assert.Contains("\"isVisited\":true", await restoredPoint.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportedOsmProviderRequiresAuthenticationAndPublishesLicensedGeoJsonWithoutVisitData()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var provider = await context.StampingProviders.SingleAsync(item =>
                item.Id == StampingProvider.HarzerWandernadelId);
            provider.IsAnonymousAccessAllowed = true;
            provider.DataSourceUri = new Uri("https://www.openstreetmap.org/relation/148007");
            provider.DataSourceAttribution = "© OpenStreetMap contributors";
            provider.DataLicenseName = "Open Data Commons Open Database License (ODbL) 1.0";
            provider.DataLicenseUri = new Uri("https://opendatacommons.org/licenses/odbl/1-0/");
            provider.DataSourceRevision = "44";
            provider.DataSourceUpdatedAt = new DateTime(2026, 3, 9, 22, 17, 30, DateTimeKind.Utc);
            provider.DataImportedAt = new DateTime(2026, 8, 30, 20, 30, 0, DateTimeKind.Utc);
            await context.SaveChangesAsync();
        }

        using var client = CreateClient(_factory);
        var anonymousGeoJsonResponse = await client.GetAsync("/api/providers/harzer-wandernadel/points.geojson");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousGeoJsonResponse.StatusCode);

        await LoginAsync(client);
        var providers = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        var response = await client.GetAsync("/api/providers/harzer-wandernadel/points.geojson");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.NotNull(providers);
        var hwn = Assert.Single(providers.StampingProviders,
            provider => provider.Slug == StampingProvider.HarzerWandernadelSlug);
        Assert.True(hwn.IsAnonymousAccessAllowed);
        Assert.Equal("© OpenStreetMap contributors", hwn.DataSourceAttribution);
        Assert.Equal("https://www.openstreetmap.org/relation/148007", hwn.DataSourceUrl);
        Assert.True(hwn.HasPublicDataDownload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/geo+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("FeatureCollection", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("44", document.RootElement.GetProperty("sourceRevision").GetString());
        Assert.Equal("© OpenStreetMap contributors", document.RootElement.GetProperty("attribution").GetString());
        var feature = Assert.Single(document.RootElement.GetProperty("features").EnumerateArray());
        Assert.Equal(RestrictedProviderPointNumber, feature.GetProperty("properties").GetProperty("number").GetInt32());
        Assert.Equal(11.8m, feature.GetProperty("geometry").GetProperty("coordinates")[0].GetDecimal());
        Assert.DoesNotContain("visited", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CookieSessionCanCreateEditAndDeleteVisitWithOptionalTimestamp()
    {
        var endpoint = $"/api/points/{WritablePointNumber}?provider={StampingProvider.TouringenSlug}";
        using var anonymousClient = CreateClient(_factory);
        var anonymousResponse = await anonymousClient.PutAsJsonAsync(
            endpoint,
            new SaveVisitRequest(null, null));
        var anonymousPatchResponse = await anonymousClient.PatchAsJsonAsync(
            endpoint,
            new SaveVisitRequest(null, null));
        var anonymousDeleteResponse = await anonymousClient.DeleteAsync(endpoint);

        using var cookieClient = CreateClient(_factory);
        await LoginAsync(cookieClient);
        var createResponse = await cookieClient.PutAsJsonAsync(endpoint, new SaveVisitRequest(null, null));
        var visitWithoutDate = await cookieClient.GetFromJsonAsync<VisitDto>(endpoint);
        var visitedPointsWithoutDate = await cookieClient.GetFromJsonAsync<GetStampingPointsResponse>(
            $"/api/points?provider={StampingProvider.TouringenSlug}&vis=true");
        var duplicateResponse = await cookieClient.PutAsJsonAsync(endpoint, new SaveVisitRequest(null, null));

        var visitedOn = new DateOnly(2026, 8, 28);
        var dateOnlyResponse = await cookieClient.PatchAsJsonAsync(endpoint, new SaveVisitRequest(visitedOn, null));
        var dateOnlyVisit = await cookieClient.GetFromJsonAsync<VisitDto>(endpoint);

        var visitedAt = new TimeOnly(14, 30);
        var dateAndTimeResponse = await cookieClient.PatchAsJsonAsync(endpoint, new SaveVisitRequest(visitedOn, visitedAt));
        var datedVisit = await cookieClient.GetFromJsonAsync<VisitDto>(endpoint);

        var deleteResponse = await cookieClient.DeleteAsync(endpoint);
        var deletedVisit = await cookieClient.GetFromJsonAsync<VisitDto>(endpoint);
        var repeatedDeleteResponse = await cookieClient.DeleteAsync(endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPatchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDeleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, createResponse.StatusCode);
        Assert.NotNull(visitWithoutDate);
        Assert.True(visitWithoutDate.IsVisited);
        Assert.Null(visitWithoutDate.VisitedOn);
        Assert.Null(visitWithoutDate.VisitedAt);
        Assert.True(visitWithoutDate.StampingPoint.IsVisited);
        Assert.NotNull(visitedPointsWithoutDate);
        Assert.Contains(visitedPointsWithoutDate.StampingPoints, point =>
            point.Number == WritablePointNumber && point.IsVisited && point.VisitedOn is null && point.VisitedAt is null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, dateOnlyResponse.StatusCode);
        Assert.NotNull(dateOnlyVisit);
        Assert.True(dateOnlyVisit.IsVisited);
        Assert.Equal(visitedOn, dateOnlyVisit.VisitedOn);
        Assert.Null(dateOnlyVisit.VisitedAt);
        Assert.Equal(HttpStatusCode.NoContent, dateAndTimeResponse.StatusCode);
        Assert.NotNull(datedVisit);
        Assert.Equal(visitedOn, datedVisit.VisitedOn);
        Assert.Equal(visitedAt, datedVisit.VisitedAt);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(deletedVisit);
        Assert.False(deletedVisit.IsVisited);
        Assert.Null(deletedVisit.VisitedOn);
        Assert.Null(deletedVisit.VisitedAt);
        Assert.Equal(HttpStatusCode.NotFound, repeatedDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task CookieSessionCanSynchronizeVisitStateAtomicallyAndIdempotently()
    {
        int pointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            pointId = await context.StampingPoints
                .Where(point => point.Number == WritablePointNumber &&
                                point.ProviderId == StampingProvider.TouringenId)
                .Select(point => point.Id)
                .SingleAsync();
        }

        var endpoint = $"/api/points/id/{pointId}/state?provider={StampingProvider.TouringenSlug}";
        var open = new VisitStateRequest(false, null, null);
        var dateOnly = new VisitStateRequest(true, new DateOnly(2026, 8, 28), null);
        var dated = new VisitStateRequest(true, new DateOnly(2026, 8, 28), new TimeOnly(14, 30));

        using var anonymousClient = CreateClient(_factory);
        var anonymousResponse = await anonymousClient.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, dateOnly));

        using var client = CreateClient(_factory);
        await LoginAsync(client);
        var createResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, dateOnly));
        var created = await createResponse.Content.ReadFromJsonAsync<VisitDto>();

        var repeatedResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, dateOnly));
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<VisitDto>();

        var conflictResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, dated));
        var conflict = await conflictResponse.Content.ReadFromJsonAsync<VisitDto>();

        var updateResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(dateOnly, dated));
        var updated = await updateResponse.Content.ReadFromJsonAsync<VisitDto>();

        var deleteResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(dated, open));
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<VisitDto>();

        var repeatedDeleteResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(dated, open));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.True(created!.IsVisited);
        Assert.Equal(dateOnly.VisitedOn, created.VisitedOn);
        Assert.Null(created.VisitedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(created with { StampingPoint = repeated!.StampingPoint }, repeated);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.True(conflict!.IsVisited);
        Assert.Equal(dateOnly.VisitedOn, conflict.VisitedOn);
        Assert.Null(conflict.VisitedAt);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(dated.VisitedOn, updated!.VisitedOn);
        Assert.Equal(dated.VisitedAt, updated.VisitedAt);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.False(deleted!.IsVisited);
        Assert.Null(deleted.VisitedOn);
        Assert.Null(deleted.VisitedAt);
        Assert.Equal(HttpStatusCode.OK, repeatedDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task VisitStateSynchronizationValidatesStateShapesAndFutureValues()
    {
        int pointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            pointId = await context.StampingPoints
                .Where(point => point.Number == WritablePointNumber &&
                                point.ProviderId == StampingProvider.TouringenId)
                .Select(point => point.Id)
                .SingleAsync();
        }

        using var client = CreateClient(_factory);
        await LoginAsync(client);
        var endpoint = $"/api/points/id/{pointId}/state?provider={StampingProvider.TouringenSlug}";
        var open = new VisitStateRequest(false, null, null);

        var openWithDateResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, new VisitStateRequest(false, new DateOnly(2026, 8, 28), null)));
        var timeWithoutDateResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(open, new VisitStateRequest(true, null, new TimeOnly(10, 30))));
        var futureResponse = await client.PutAsJsonAsync(
            endpoint,
            new SynchronizeVisitRequest(
                open,
                new VisitStateRequest(true, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), null),
                0));

        Assert.Equal(HttpStatusCode.BadRequest, openWithDateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, timeWithoutDateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futureResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentVisitStateSynchronizationAllowsOnlyOneExpectedStateTransition()
    {
        int pointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            pointId = await context.StampingPoints
                .Where(point => point.Number == WritablePointNumber &&
                                point.ProviderId == StampingProvider.TouringenId)
                .Select(point => point.Id)
                .SingleAsync();
        }

        using var firstClient = CreateClient(_factory);
        using var secondClient = CreateClient(_factory);
        await LoginAsync(firstClient);
        await LoginAsync(secondClient);
        var endpoint = $"/api/points/id/{pointId}/state?provider={StampingProvider.TouringenSlug}";
        var expected = new VisitStateRequest(false, null, null);
        var firstDesired = new VisitStateRequest(true, new DateOnly(2026, 8, 27), null);
        var secondDesired = new VisitStateRequest(true, new DateOnly(2026, 8, 28), null);

        var responses = await Task.WhenAll(
            firstClient.PutAsJsonAsync(endpoint, new SynchronizeVisitRequest(expected, firstDesired)),
            secondClient.PutAsJsonAsync(endpoint, new SynchronizeVisitRequest(expected, secondDesired)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var states = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<VisitDto>()));
        Assert.All(states, state => Assert.NotNull(state));
        Assert.Equal(states[0]!.VisitedOn, states[1]!.VisitedOn);
    }

    [Fact]
    public async Task StablePointIdCanRecordVisitForUnnumberedTemporarySpecial()
    {
        int pointId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var point = new StampingPoint(
                default,
                "Temporary special",
                11.3m,
                50.6m,
                null,
                0,
                StampingProvider.TouringenId,
                "temporary-special-2026")
            {
                SeriesId = StampingSeries.TouringenSpecialStampsId,
                ValidFrom = new DateOnly(2026, 6, 1)
            };
            context.StampingPoints.Add(point);
            await context.SaveChangesAsync();
            pointId = point.Id;
        }

        using var client = CreateClient(_factory);
        await LoginAsync(client);
        var response = await client.PutAsJsonAsync(
            $"/api/points/id/{pointId}?provider={StampingProvider.TouringenSlug}",
            new SaveVisitRequest(null, null));
        var points = await client.GetFromJsonAsync<GetStampingPointsResponse>("/api/points?provider=all&vis=true");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var special = Assert.Single(points!.StampingPoints, point => point.Id == pointId);
        Assert.Null(special.Number);
        Assert.Equal(StampingSeries.TouringenSpecialStampsSlug, special.Series.Slug);
        Assert.True(special.IsVisited);
    }

    [Fact]
    public async Task VisitTimestampValidationRejectsTimeWithoutDateAndFutureValues()
    {
        using var client = CreateClient(_factory);
        await LoginAsync(client);
        var endpoint = $"/api/points/{WritablePointNumber}?provider={StampingProvider.TouringenSlug}";

        var timeOnlyResponse = await client.PutAsJsonAsync(endpoint, new SaveVisitRequest(null, new TimeOnly(10, 30)));
        var futureResponse = await client.PutAsJsonAsync(
            endpoint,
            new SaveVisitRequest(DateOnly.FromDateTime(DateTime.Now.AddDays(2)), null));

        Assert.Equal(HttpStatusCode.BadRequest, timeOnlyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futureResponse.StatusCode);
    }

    [Fact]
    public async Task BundledFrontendUsesSessionRoutesWithoutUserIdentityOrTokenStorage()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var script = await client.GetStringAsync("/js/toured.js");
        var normalizedFrontend = $"{html}\n{script}".ToLowerInvariant();

        Assert.Contains("href=\"auth/login\"", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("auth/session", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("auth/logout", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("api/providers", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("api/points?provider=all", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("api/points?provider=all&vis=false", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("api/points?provider=all&vis=true", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("pointcache", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("selectedproviderslugs", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("renderselectedpoints", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("setselectedproviders", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("loadgeneration", normalizedFrontend, StringComparison.Ordinal);
        Assert.Contains("window.history.replacestate", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("userid", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("toured-user", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("localstorage", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionstorage", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", normalizedFrontend, StringComparison.Ordinal);
        Assert.DoesNotContain("googlesubject", normalizedFrontend, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendIsResponsiveTouchFriendlyAndMoreAccessible()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");
        var neutralPinResponse = await client.GetAsync("/img/pin_icon_neutral.svg");
        var neutralPin = await neutralPinResponse.Content.ReadAsStringAsync();
        var visitedPinResponse = await client.GetAsync("/img/pin_icon_visited.svg");
        var visitedPin = await visitedPinResponse.Content.ReadAsStringAsync();
        var logoResponse = await client.GetAsync("/img/toured-logo-transparent.svg");

        Assert.Contains("<html lang=\"de\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("viewport-fit=cover", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role=\"dialog\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Details schließen\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"brand-logo\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=\"brand-logo\" aria-hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src=\"img/toured-logo-transparent.svg\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"accountMenuButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-controls=\"accountPanel\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"accountPanel\" class=\"account-panel\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"mapLegend\" aria-label=\"Kartenlegende\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height: 100dvh", css, StringComparison.Ordinal);
        Assert.Contains("[hidden]", css, StringComparison.Ordinal);
        Assert.Contains("env(safe-area-inset-bottom)", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains(".account-panel", css, StringComparison.Ordinal);
        Assert.Contains(".brand-logo", css, StringComparison.Ordinal);
        Assert.Contains("(hover: hover) and (pointer: fine)", css, StringComparison.Ordinal);
        Assert.Contains("window.matchMedia(\"(hover: hover) and (pointer: fine)\")", script, StringComparison.Ordinal);
        Assert.Contains("event.key !== \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains("hitTolerance", script, StringComparison.Ordinal);
        Assert.Contains("scale: 0.32", script, StringComparison.Ordinal);
        Assert.Contains("img/pin_icon_neutral.svg?v=3", script, StringComparison.Ordinal);
        Assert.Contains("img/pin_icon_visited.svg?v=3", script, StringComparison.Ordinal);
        Assert.Contains("attribution: false, zoom: false", script, StringComparison.Ordinal);
        Assert.Contains("VisitState.unknown", script, StringComparison.Ordinal);
        Assert.Contains("elements.mapLegend.hidden = !authenticated", script, StringComparison.Ordinal);
        Assert.Contains("closeAccountMenu(true)", script, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, neutralPinResponse.StatusCode);
        Assert.Equal("image/svg+xml", neutralPinResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("width=\"94\" height=\"128\"", neutralPin, StringComparison.Ordinal);
        Assert.Contains("#168BD2", neutralPin, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, visitedPinResponse.StatusCode);
        Assert.Equal("image/svg+xml", visitedPinResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("#123E65", visitedPin, StringComparison.Ordinal);
        Assert.Contains("stroke=\"#fff\"", visitedPin, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
        Assert.Equal("image/svg+xml", logoResponse.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("pin_icon_red.png", script, StringComparison.Ordinal);
        Assert.DoesNotContain("pin_icon_green.png", script, StringComparison.Ordinal);
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendOffersAccessibleProviderFilteringAndInformation()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");

        Assert.Contains("id=\"providerMenuButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-controls=\"providerPanel\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"providerPanel\" class=\"account-panel provider-panel\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<fieldset class=\"provider-fieldset\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"selectAllProvidersButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"selectNoProvidersButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<dialog id=\"providerInfoDialog\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"providerDataSource\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stempelstellen als GeoJSON herunterladen", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TourEd ist ein unabhängiges Projekt", html, StringComparison.Ordinal);
        Assert.Contains("id=\"pointProvider\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".provider-info-button", css, StringComparison.Ordinal);
        Assert.Contains("width: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains("aria-haspopup", script, StringComparison.Ordinal);
        Assert.Contains("renderProviderOptions", script, StringComparison.Ordinal);
        Assert.Contains("applyProviderSelection", script, StringComparison.Ordinal);
        Assert.Contains("setSelectedProviders(selectedSlugs)", script, StringComparison.Ordinal);
        Assert.Contains("closeProviderMenu(true)", script, StringComparison.Ordinal);
        Assert.Contains("closeAccountMenu()", script, StringComparison.Ordinal);
        Assert.Contains("elements.providerInfoDialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("providerInfoDialog.addEventListener(\"cancel\"", script, StringComparison.Ordinal);
        Assert.Contains("trigger?.isConnected", script, StringComparison.Ordinal);
        Assert.Contains("elements.progressButton.focus", script, StringComparison.Ordinal);
        Assert.Contains("url.protocol === \"http:\" || url.protocol === \"https:\"", script, StringComparison.Ordinal);
        Assert.Contains("provider.hasPublicDataDownload", script, StringComparison.Ordinal);
        Assert.Contains("points.geojson", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.provider?.name", script, StringComparison.Ordinal);
        Assert.Contains("getPointNumberLabel(stampingPoint)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BundledFrontendOffersAccessibleSelectedProviderFullTextSearch()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");

        var locateButtonPosition = html.IndexOf("id=\"locateButton\"", StringComparison.OrdinalIgnoreCase);
        var visitFilterButtonPosition = html.IndexOf("id=\"visitFilterButton\"", StringComparison.OrdinalIgnoreCase);
        var searchButtonPosition = html.IndexOf("id=\"searchMenuButton\"", StringComparison.OrdinalIgnoreCase);
        var providerButtonPosition = html.IndexOf("id=\"providerMenuButton\"", StringComparison.OrdinalIgnoreCase);
        Assert.True(locateButtonPosition >= 0 && locateButtonPosition < visitFilterButtonPosition);
        Assert.True(visitFilterButtonPosition >= 0 && visitFilterButtonPosition < searchButtonPosition);
        Assert.True(searchButtonPosition >= 0 && searchButtonPosition < providerButtonPosition);
        Assert.Contains("aria-label=\"Auf meinen Standort zentrieren\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tracking: false", script, StringComparison.Ordinal);
        Assert.Contains("geolocation.setTracking(true)", script, StringComparison.Ordinal);
        Assert.Contains("geolocation.setTracking(false)", script, StringComparison.Ordinal);
        Assert.Contains("altShiftDragRotate: false", script, StringComparison.Ordinal);
        Assert.Contains("pinchRotate: false", script, StringComparison.Ordinal);
        Assert.Contains("enableRotation: false", script, StringComparison.Ordinal);
        Assert.Contains("data-visit-filter=\"all\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Besuchsfilter: Alle Stempelstellen", html, StringComparison.Ordinal);
        Assert.Contains("const VisitFilterOrder = [VisitFilter.all, VisitFilter.open, VisitFilter.visited]", script, StringComparison.Ordinal);
        Assert.Contains("isVisitStateVisible(visitState)", script, StringComparison.Ordinal);
        Assert.Contains("elements.visitFilterButton.addEventListener(\"click\", cycleVisitFilter)", script, StringComparison.Ordinal);
        Assert.Contains("app.visitFilter = VisitFilter.all", script, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"searchPanel\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Stempelstellensuche öffnen\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<circle cx=\"10.5\" cy=\"10.5\" r=\"6.5\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"searchPanel\" class=\"account-panel search-panel\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"stampingPointSearchInput\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type=\"search\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"searchResultsStatus\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role=\"status\" aria-live=\"polite\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"searchResults\" class=\"search-results\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".search-panel", css, StringComparison.Ordinal);
        Assert.Contains(".search-result-button", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains("const SearchResultLimit = 30", script, StringComparison.Ordinal);
        Assert.Contains("normalize(\"NFD\")", script, StringComparison.Ordinal);
        Assert.Contains("app.selectedProviderSlugs.has(point.provider?.slug)", script, StringComparison.Ordinal);
        Assert.Contains("point.provider?.abbreviation", script, StringComparison.Ordinal);
        Assert.Contains("queryTokens.every", script, StringComparison.Ordinal);
        Assert.Contains("renderSearchResults", script, StringComparison.Ordinal);
        Assert.Contains("closeSearchMenu(true)", script, StringComparison.Ordinal);
        Assert.Contains("view.animate({ center: coordinate, zoom, duration: 250 }", script, StringComparison.Ordinal);
        Assert.Contains("showInfo(feature, pixel, true)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendOffersStableShareablePointLinks()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");

        Assert.Contains("id=\"copyPointLinkButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"pointShareStatus\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Link kopieren", html, StringComparison.Ordinal);
        Assert.Contains(".point-share-controls", css, StringComparison.Ordinal);
        Assert.Contains("params.get(\"provider\")", script, StringComparison.Ordinal);
        Assert.Contains("params.get(\"point\")", script, StringComparison.Ordinal);
        Assert.Contains("Number.isSafeInteger(pointId)", script, StringComparison.Ordinal);
        Assert.Contains("candidate.id === pointLink.pointId", script, StringComparison.Ordinal);
        Assert.Contains("candidate.provider?.slug === pointLink.providerSlug", script, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard?.writeText", script, StringComparison.Ordinal);
        Assert.Contains("auth/login?returnUrl=", script, StringComparison.Ordinal);
        Assert.Contains("openPendingPointLink()", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendClustersVisiblePointsClientSide()
    {
        using var client = CreateClient(_factory);

        var script = await client.GetStringAsync("/js/toured.js");

        Assert.Contains("new ol.source.Cluster", script, StringComparison.Ordinal);
        Assert.Contains("distance: 44", script, StringComparison.Ordinal);
        Assert.Contains("source: markerSource", script, StringComparison.Ordinal);
        Assert.Contains("app.markerSource.addFeatures(features)", script, StringComparison.Ordinal);
        Assert.Contains("features.length > 99 ? \"99+\"", script, StringComparison.Ordinal);
        Assert.Contains("getClusterVisitState", script, StringComparison.Ordinal);
        Assert.Contains("linearGradient id=\"mixed\"", script, StringComparison.Ordinal);
        Assert.Contains("layerFilter: layer => layer === app.markerLayer", script, StringComparison.Ordinal);
        Assert.Contains("zoomToCluster", script, StringComparison.Ordinal);
        Assert.Contains("duration: reducedMotion.matches ? 0 : 450", script, StringComparison.Ordinal);
        Assert.Contains("maxZoom: targetMaxZoom", script, StringComparison.Ordinal);
        Assert.Contains("app.map.on(\"moveend\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendSupportsAccessibleVisitCreationEditingAndConfirmedDeletion()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");

        Assert.Contains("id=\"visitNowButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jetzt stempeln", html, StringComparison.Ordinal);
        Assert.Contains("class=\"stamp-logo-icon\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".action--tile .stamp-logo-icon", css, StringComparison.Ordinal);
        Assert.Contains("fill: currentColor", css, StringComparison.Ordinal);
        Assert.Contains("id=\"openVisitFormButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nachtragen", html, StringComparison.Ordinal);
        Assert.Contains("id=\"editVisitButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bearbeiten", html, StringComparison.Ordinal);
        Assert.Contains("id=\"deleteVisitButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Entfernen", html, StringComparison.Ordinal);
        Assert.Contains("id=\"visitedOnInput\" name=\"visitedOn\" type=\"date\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"visitedAtInput\" name=\"visitedAt\" type=\"time\" disabled", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ohne Angaben wird nur der Eintrag gespeichert.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"deleteVisitDialog\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stempeleintrag endgültig entfernen", html, StringComparison.Ordinal);
        Assert.Contains("id=\"pendingVisitIndicator\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Synchronisierung ausstehend", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\" aria-live=\"polite\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".visit-controls", css, StringComparison.Ordinal);
        Assert.Contains(".pending-visit-indicator", css, StringComparison.Ordinal);
        Assert.Contains("top: 0.65rem", css, StringComparison.Ordinal);
        Assert.Contains("left: 0.65rem", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains("queueVisitAction(feature", script, StringComparison.Ordinal);
        Assert.Contains("api/points/id/${action.pointId}/state?provider=${provider}", script, StringComparison.Ordinal);
        Assert.Contains("expected: action.expected", script, StringComparison.Ordinal);
        Assert.Contains("desired: action.desired", script, StringComparison.Ordinal);
        Assert.Contains("isVisited: false", script, StringComparison.Ordinal);
        Assert.Contains("elements.deleteVisitDialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("wirklich entfernt werden", script, StringComparison.Ordinal);
        Assert.Contains("id=\"authBarrier\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<main id=\"appShell\" inert aria-hidden=\"true\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"authBarrier\" class=\"auth-barrier\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"authBarrierTitle\" aria-describedby=\"authBarrierDesc\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".auth-barrier", css, StringComparison.Ordinal);
        Assert.Contains("width: min(14rem, 80%);", css, StringComparison.Ordinal);
        Assert.Contains("showAuthBarrier", script, StringComparison.Ordinal);
        Assert.Contains("hideAuthBarrier", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("const registrationParam = urlParams.get(\"registration\")", StringComparison.Ordinal) <
            script.IndexOf("window.history.replaceState", StringComparison.Ordinal));
        Assert.Contains("id=\"authBarrierLoading\" class=\"auth-barrier__loading\" role=\"status\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span>Sitzung wird geprüft …</span>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"authBarrierLoginButton\" class=\"action action--primary auth-barrier__login\" href=\"auth/login\" hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("elements.authBarrierLoginButton.hidden = true", script, StringComparison.Ordinal);
        Assert.Contains("elements.authBarrierLoginButton.hidden = false", script, StringComparison.Ordinal);
        Assert.Contains("elements.authBarrierLoading.hidden = registrationDecisionVisible", script, StringComparison.Ordinal);
        Assert.Contains("elements.authBarrierDesc.hidden = registrationDecisionVisible", script, StringComparison.Ordinal);
        Assert.Contains("Solange diese Entscheidung gespeichert ist", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFrontendDeclaresEveryReferencedDomElement()
    {
        using var client = CreateClient(_factory);
        var script = await client.GetStringAsync("/js/toured.js");
        var registry = Regex.Match(
            script,
            @"const elements = \{(?<body>.*?)^\s{4}\};",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(registry.Success);
        var declarations = Regex.Matches(
                registry.Groups["body"].Value,
                @"^\s*(?<name>[A-Za-z_$][\w$]*):",
                RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        declarations.UnionWith(Regex.Matches(
                script,
                @"\belements\.(?<name>[A-Za-z_$][\w$]*)\s*=(?!=)")
            .Select(match => match.Groups["name"].Value));
        var references = Regex.Matches(script, @"\belements\.(?<name>[A-Za-z_$][\w$]*)\b")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(references.Except(declarations));
    }

    [Fact]
    public async Task ToursEndpointRequiresAuthentication()
    {
        using var client = CreateClient(_factory);

        var anonymousResponse = await client.GetAsync("/api/tours");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        await LoginAsync(client);
        var authenticatedResponse = await client.GetAsync("/api/tours");
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    [Fact]
    public async Task PrivacyNoticeIsPublicLinkedAndExcludedFromSearchIndexing()
    {
        using var client = CreateClient(_factory);

        var frontendScript = await client.GetStringAsync("/js/toured.js");
        var privacyResponse = await client.GetAsync("/datenschutz/");
        var privacyNotice = await privacyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
        Assert.Contains("href=\"https://github.com/ElPlatero/TourEd\"", frontendScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"TourEd-Quellcode auf GitHub (AGPL-3.0)\"", frontendScript, StringComparison.Ordinal);
        Assert.Contains(">&copy; TourEd</a> · <a class=\"footer-link\" href=\"datenschutz/\">Datenschutz</a>", frontendScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TourEd 2023", frontendScript, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", frontendScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow, noarchive\"", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("info@toured-app.de", privacyNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("dsgvo@baelgun.de", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@schuettpelz.org", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Die Funktion „Abmelden“ beendet nur die aktuelle Sitzung", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Eine Verbindung zu dieser Website wird erst hergestellt, wenn der Link bewusst geöffnet wird", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("TourEd übermittelt beim Öffnen eines Anbieterlinks weder die E-Mail-Adresse noch gespeicherte Stempelbesuche", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("erst nach Betätigung der Standort-Schaltfläche aktiviert", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("nicht an das TourEd-Backend", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Die Nutzung ist freiwillig", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("widerrufene Standortberechtigung beeinträchtigt die übrige Kartenfunktion nicht", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("OpenStreetMap-Kacheln für den angezeigten Kartenausschnitt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("ungefähren angezeigten Standort ableiten", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("TourEd ist ein unabhängiges Projekt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Beim bewussten Öffnen eines externen Anbieterlinks", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("serverseitig aus OpenStreetMap übernommen", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("keine Konto-, Sitzungs- oder Besuchsdaten an OpenStreetMap", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Quellcode-Link führt zum öffentlichen TourEd-Repository auf GitHub", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("weder die E-Mail-Adresse noch gespeicherte Stempelbesuche an GitHub", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("GeoJSON-Download", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("keine Konto- oder Besuchsdaten an den Stempelanbieter übermittelt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Ein Besuch kann auch ohne Datum und Uhrzeit eingetragen werden", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("den einzelnen Besuch nach einer Bestätigung vollständig löschen", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("die freigeschalteten Stempelanbieter", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Registrierungsantrag", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Die Bereinigung läuft nach dem Anwendungsstart und anschließend täglich", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("kann das betroffene Google-Konto keinen neuen Antrag stellen", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("administrative Änderungen an Anbieterfreigaben", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Google-Kontokennungen und E-Mail-Adressen werden nicht in dieses Änderungsprotokoll", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Mit der Kontolöschung werden die Anbieterfreigaben", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Lokale Speicherung, Offline-Nutzung und Service Worker", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("IndexedDB", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("noch nicht synchronisierte Stempelaktionen", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("automatisch mit dem angemeldeten Konto synchronisiert", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("nicht zusätzlich anwendungsseitig verschlüsselt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("hängt vom Schutz des jeweiligen Endgeräts", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("bei einem Wechsel des Benutzerkontos, bei einer nicht mehr gültigen serverseitigen Sitzung", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("ausdrücklich keine OpenStreetMap-Kartenkacheln im Service-Worker-Cache", privacyNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("Die anonyme Kartenansicht ist ohne Benutzerkonto möglich", privacyNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("Google Hosted Libraries", privacyNotice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebManifestIsServedCorrectlyAndMatchesPwaContract()
    {
        using var client = CreateClient(_factory);

        var response = await client.GetAsync("/manifest.webmanifest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var manifestJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;

        Assert.Equal("TourEd", root.GetProperty("name").GetString());
        Assert.Equal("TourEd", root.GetProperty("short_name").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("de", root.GetProperty("lang").GetString());
        Assert.Equal("./", root.GetProperty("start_url").GetString());
        Assert.Equal("./", root.GetProperty("scope").GetString());
        Assert.Equal("#f7f5ef", root.GetProperty("theme_color").GetString());
        Assert.Equal("#f7f5ef", root.GetProperty("background_color").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.NotEmpty(icons);

        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "img/icon-192.png" &&
            icon.GetProperty("sizes").GetString() == "192x192" &&
            icon.GetProperty("purpose").GetString() == "any");

        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "img/icon-512.png" &&
            icon.GetProperty("sizes").GetString() == "512x512" &&
            icon.GetProperty("purpose").GetString() == "any");

        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "img/icon-maskable-512.png" &&
            icon.GetProperty("sizes").GetString() == "512x512" &&
            icon.GetProperty("purpose").GetString() == "maskable");
    }

    [Fact]
    public async Task ServiceWorkerIsServedAndImplementsSecurityAndCachingRules()
    {
        using var client = CreateClient(_factory);

        var response = await client.GetAsync("/service-worker.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var swScript = await response.Content.ReadAsStringAsync();

        // Core caching rules
        Assert.Contains("toured-shell-v13", swScript, StringComparison.Ordinal);
        Assert.Contains("css/toured.css", swScript, StringComparison.Ordinal);
        Assert.Contains("js/toured.js", swScript, StringComparison.Ordinal);
        Assert.Contains("manifest.webmanifest", swScript, StringComparison.Ordinal);
        Assert.Contains("img/toured-logo-transparent.svg", swScript, StringComparison.Ordinal);
        Assert.Contains("img/pin_icon_neutral.svg", swScript, StringComparison.Ordinal);
        Assert.Contains("img/pin_icon_visited.svg", swScript, StringComparison.Ordinal);
        Assert.Contains("img/icon-192.png", swScript, StringComparison.Ordinal);
        Assert.Contains("img/icon-512.png", swScript, StringComparison.Ordinal);
        Assert.Contains("img/icon-maskable-512.png", swScript, StringComparison.Ordinal);
        Assert.Contains("datenschutz/", swScript, StringComparison.Ordinal);

        // Exact OpenLayers assets
        Assert.Contains("https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/css/ol.css", swScript, StringComparison.Ordinal);
        Assert.Contains("https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/build/ol.js", swScript, StringComparison.Ordinal);

        // Security boundaries: valid OAuth callbacks and all other auth/api/health requests bypass the cache.
        // A stale callback navigation without OAuth state is redirected to the app root.
        Assert.Contains("/auth/", swScript, StringComparison.Ordinal);
        Assert.Contains("/signin-google", swScript, StringComparison.Ordinal);
        Assert.Contains("event.request.mode === \"navigate\" && !url.searchParams.has(\"state\")", swScript, StringComparison.Ordinal);
        Assert.Contains("Response.redirect(new URL(\"./\", url).href, 302)", swScript, StringComparison.Ordinal);
        Assert.Contains("/api/", swScript, StringComparison.Ordinal);
        Assert.Contains("/health", swScript, StringComparison.Ordinal);
        Assert.Contains("event.request.method !== \"GET\"", swScript, StringComparison.Ordinal);

        // Strict OSM tile exclusion
        Assert.Contains("tile.openstreetmap.org", swScript, StringComparison.Ordinal);
        Assert.Contains("if (!CACHEABLE_URLS.has(event.request.url))", swScript, StringComparison.Ordinal);
        Assert.DoesNotContain("caches.match(", swScript, StringComparison.Ordinal);
        Assert.Contains("const cached = await cache.match(event.request)", swScript, StringComparison.Ordinal);
        Assert.Contains("await cache.put(event.request, response.clone())", swScript, StringComparison.Ordinal);
        Assert.Contains("await caches.delete(CACHE_NAME)", swScript, StringComparison.Ordinal);

        // Update handling with SKIP_WAITING
        Assert.Contains("SKIP_WAITING", swScript, StringComparison.Ordinal);
        Assert.Contains("event.waitUntil(self.skipWaiting())", swScript, StringComparison.Ordinal);
        Assert.Contains("await self.clients.claim()", swScript, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/img/icon-192.png", "image/png")]
    [InlineData("/img/icon-512.png", "image/png")]
    [InlineData("/img/icon-maskable-512.png", "image/png")]
    [InlineData("/img/apple-touch-icon.png", "image/png")]
    [InlineData("/favicon.ico", "image/x-icon")]
    public async Task PwaIconsAreServedSuccessfully(string path, string expectedMediaType)
    {
        using var client = CreateClient(_factory);

        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType.MediaType);
        Assert.True((response.Content.Headers.ContentLength ?? 0) > 0);
    }

    [Fact]
    public async Task IndexHtmlContainsPwaElementsAndIntegrations()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("<link rel=\"manifest\" href=\"manifest.webmanifest\"", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"icon\" type=\"image/x-icon\" href=\"favicon.ico\"", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"icon\" type=\"image/png\" sizes=\"192x192\" href=\"img/icon-192.png\"", html, StringComparison.Ordinal);
        Assert.Contains("<link rel=\"apple-touch-icon\" href=\"img/apple-touch-icon.png\"", html, StringComparison.Ordinal);

        Assert.Contains("id=\"offlineBadge\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"tileErrorBanner\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"offlineNotice\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"updatePrompt\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"updateReloadButton\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"brand-logo\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"brand-logo\" aria-hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"updatePrompt\" class=\"update-prompt\" hidden role=\"status\" aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FrontendUsesIndexedDbAndNeverUsesLocalStorageOrSessionStorage()
    {
        using var client = CreateClient(_factory);

        var script = await client.GetStringAsync("/js/toured.js");

        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.Ordinal);

        Assert.Contains("DB_NAME = \"toured-db\"", script, StringComparison.Ordinal);
        Assert.Contains("indexedDB.open(DB_NAME", script, StringComparison.Ordinal);
        Assert.Contains("\"snapshots\"", script, StringComparison.Ordinal);
        Assert.Contains("\"current\"", script, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT_SCHEMA_VERSION = 3", script, StringComparison.Ordinal);
        Assert.Contains("pendingActions", script, StringComparison.Ordinal);
        Assert.Contains("Synchronisierung ausstehend", await client.GetStringAsync("/"), StringComparison.Ordinal);
        Assert.Contains("BroadcastChannel", script, StringComparison.Ordinal);
        Assert.Contains("acquireSyncLease", script, StringComparison.Ordinal);
        Assert.Contains("synchronizePendingActions", script, StringComparison.Ordinal);
        Assert.Contains("scheduleSynchronizationRetry", script, StringComparison.Ordinal);
        Assert.Contains("window.confirm(\"Nicht synchronisierte Stempeländerungen", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"focus\", () =>", script, StringComparison.Ordinal);
        Assert.Contains("if (!elements.visitForm.hidden || elements.deleteVisitDialog.open) return;", script, StringComparison.Ordinal);
        Assert.Contains("queueSnapshotOperation", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"offline\"", script, StringComparison.Ordinal);
        Assert.Contains("scheduleSessionExpiry", script, StringComparison.Ordinal);
        Assert.Contains("showOfflineUnavailable", script, StringComparison.Ordinal);
        Assert.Contains("updateReloadRequested", script, StringComparison.Ordinal);
        Assert.Contains("isGoogleCallbackPath(window.location.pathname)", script, StringComparison.Ordinal);
        Assert.Contains("window.location.replace(getAppRootUrl().href)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("getStoredSnapshot().then", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousSessionDoesNotIncludeExpiresAt()
    {
        using var client = CreateClient(_factory);

        var response = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("expiresAt", out _));
        Assert.False(doc.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task SessionReportsTheEffectiveExpirationOfASlidingCookieRenewal()
    {
        int userId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            userId = await scope.ServiceProvider.GetRequiredService<DataContext>().Users
                .Where(user => user.Email == FakeGoogleHandler.Email)
                .Select(user => user.Id)
                .SingleAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var identity = new ClaimsIdentity(
            [
                new Claim(Constants.ClaimsNames.UserId, userId.ToString()),
                new Claim(Constants.ClaimsNames.UserEmail, FakeGoogleHandler.Email)
            ],
            TouredAuthenticationSchemes.Cookie);
        var properties = new AuthenticationProperties
        {
            IssuedUtc = now.AddHours(-5),
            ExpiresUtc = now.AddHours(3),
            AllowRefresh = true
        };
        using var client = CreateClient(_factory, handleCookies: false);
        client.DefaultRequestHeaders.Add(
            "Cookie",
            CreateSessionCookie(_factory.Services, identity, properties));

        var response = await client.GetAsync("/auth/session");
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.True(session.Authenticated);
        Assert.NotNull(session.ExpiresAt);
        Assert.InRange(session.ExpiresAt.Value, now.AddHours(7).AddMinutes(59), now.AddHours(8).AddMinutes(1));

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TouredAuthenticationSchemes.Cookie);
        var protectedTicket = setCookie.Split(';', 2)[0].Split('=', 2)[1];
        var renewedTicket = options.TicketDataFormat.Unprotect(protectedTicket);
        Assert.NotNull(renewedTicket);
        Assert.Equal(session.ExpiresAt, renewedTicket.Properties.ExpiresUtc);
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
        Assert.Null(initialSession.ExpiresAt);
        Assert.Equal(HttpStatusCode.Unauthorized, initialPoints.StatusCode);
        Assert.NotNull(authenticatedSession);
        Assert.True(authenticatedSession.Authenticated);
        Assert.NotNull(authenticatedSession.ExpiresAt);
        Assert.True(authenticatedSession.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, unvisitedPoints.StatusCode);
        Assert.Equal(HttpStatusCode.OK, visitedPoints.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.NotNull(finalSession);
        Assert.False(finalSession.Authenticated);
        Assert.Null(finalSession.ExpiresAt);
        Assert.Equal(HttpStatusCode.Unauthorized, finalPoints.StatusCode);
    }

    [Fact]
    public async Task PublicGoogleCallbackIncludesConfiguredPathBase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"toured-path-base-{Guid.NewGuid():N}.db");
        var keysPath = Path.Combine(Path.GetTempPath(), $"toured-path-base-keys-{Guid.NewGuid():N}");
        await using var factory = new TouredWebApplicationFactory(databasePath, keysPath, useFakeGoogle: false, pathBase: "/toured");
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<DataContext>().Database.MigrateAsync();
            }
            using var client = CreateClient(factory);

            var response = await client.GetAsync("/toured/auth/login");
            var privacyResponse = await client.GetAsync("/toured/datenschutz/");
            var styleResponse = await client.GetAsync("/toured/css/toured.css");
            var scriptResponse = await client.GetAsync("/toured/js/toured.js");
            var neutralPinResponse = await client.GetAsync("/toured/img/pin_icon_neutral.svg");
            var visitedPinResponse = await client.GetAsync("/toured/img/pin_icon_visited.svg");
            var logoResponse = await client.GetAsync("/toured/img/toured-logo-transparent.svg");
            var manifestResponse = await client.GetAsync("/toured/manifest.webmanifest");
            var serviceWorkerResponse = await client.GetAsync("/toured/service-worker.js");
            var appIconResponse = await client.GetAsync("/toured/img/icon-192.png");
            var providersResponse = await client.GetAsync("/toured/api/providers");
            var restrictedPointsResponse = await client.GetAsync("/toured/api/points?provider=harzer-wandernadel");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, styleResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, scriptResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, neutralPinResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, visitedPinResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, serviceWorkerResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, appIconResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, providersResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, restrictedPointsResponse.StatusCode);
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
            $"test-{providerId}-{number}")
        {
            SeriesId = providerId switch
            {
                StampingProvider.TouringenId => StampingSeries.TouringenStandardId,
                StampingProvider.HarzerWandernadelId => StampingSeries.HarzerWandernadelStandardId,
                _ => 30
            }
        };

    private static string CreateSessionCookie(
        IServiceProvider services,
        ClaimsIdentity identity,
        AuthenticationProperties? properties = null)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TouredAuthenticationSchemes.Cookie);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            properties ?? new AuthenticationProperties(),
            TouredAuthenticationSchemes.Cookie);
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
        string? pathBase = null,
        bool useStubGoogleBackchannel = false) : WebApplicationFactory<Program>
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
            else if (useStubGoogleBackchannel)
            {
                builder.ConfigureTestServices(services => services.PostConfigure<GoogleOptions>(
                    TouredAuthenticationSchemes.Google,
                    options =>
                    {
                        options.AuthorizationEndpoint = "https://google.test/authorize";
                        options.TokenEndpoint = "https://google.test/token";
                        options.UserInformationEndpoint = "https://google.test/userinfo";
                        options.Backchannel = new HttpClient(new StubGoogleBackchannelHandler());
                    }));
            }
        }
    }

    private sealed class StubGoogleBackchannelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = request.RequestUri?.AbsolutePath switch
            {
                "/token" => "{\"access_token\":\"test-access-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}",
                "/userinfo" => "{\"id\":\"google-sub-real-handler\",\"email\":\"real-handler@example.test\",\"verified_email\":true}",
                _ => throw new InvalidOperationException($"Unexpected Google backchannel request: {request.RequestUri}")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
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

            var email = Request.Query["email"].FirstOrDefault() ?? Email;
            var subject = Request.Query["subject"].FirstOrDefault() ?? "google-subject-1";
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
