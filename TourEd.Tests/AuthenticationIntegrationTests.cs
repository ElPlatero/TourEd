using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.Authentication;
using Api.Controllers.Auth;
using Api.Controllers.Points;
using Api.Controllers.Providers;
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
    private const int RestrictedProviderPointNumber = 45;
    private const int OtherProviderId = 3;
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
            Id = 4,
            Slug = "unsupported-link",
            Name = "Unsupported link provider",
            IsAnonymousAccessAllowed = true,
            Description = "Provider with a non-public website scheme.",
            WebsiteUri = new Uri("ftp://provider.example.test/info")
        });
        var user = new User { Email = FakeGoogleHandler.Email };
        var visitedPoint = CreatePoint(VisitedPointNumber, StampingProvider.TouringenId);
        context.Users.Add(user);
        context.StampingPoints.AddRange(
            visitedPoint,
            CreatePoint(UnvisitedPointNumber, StampingProvider.TouringenId),
            CreatePoint(WritablePointNumber, StampingProvider.TouringenId),
            CreatePoint(OtherProviderPointNumber, OtherProviderId),
            CreatePoint(RestrictedProviderPointNumber, StampingProvider.HarzerWandernadelId));
        await context.SaveChangesAsync();
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
    public async Task AllProviderPointQueriesWorkForAnonymousAndAuthenticatedSessions()
    {
        using var anonymousClient = CreateClient(_factory);
        var anonymousResponse = await anonymousClient.GetFromJsonAsync<GetStampingPointsResponse>(
            "/api/points?provider=all");
        var restrictedAnonymousResponse = await anonymousClient.GetAsync(
            $"/api/points?provider={StampingProvider.HarzerWandernadelSlug}");

        using var authenticatedClient = CreateClient(_factory);
        await LoginAsync(authenticatedClient);
        var visitedResponse = await authenticatedClient.GetFromJsonAsync<GetStampingPointsResponse>(
            "/api/points?provider=all&vis=true");
        var unvisitedResponse = await authenticatedClient.GetFromJsonAsync<GetStampingPointsResponse>(
            "/api/points?provider=all&vis=false");

        Assert.NotNull(anonymousResponse);
        Assert.Contains(anonymousResponse.StampingPoints, point =>
            point.Provider.Slug == StampingProvider.TouringenSlug);
        Assert.Contains(anonymousResponse.StampingPoints, point =>
            point.Provider.Slug == "other" && point.Number == OtherProviderPointNumber);
        Assert.DoesNotContain(anonymousResponse.StampingPoints, point =>
            point.Provider.Slug == StampingProvider.HarzerWandernadelSlug);
        Assert.Equal(HttpStatusCode.Unauthorized, restrictedAnonymousResponse.StatusCode);

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
    public async Task ProviderCatalogFiltersRestrictedProvidersAndExposesOnlyPublicWebsiteUrls()
    {
        using var client = CreateClient(_factory);

        var anonymousResponse = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");
        await LoginAsync(client);
        var authenticatedResponse = await client.GetFromJsonAsync<GetStampingProvidersResponse>("/api/providers");

        Assert.NotNull(anonymousResponse);
        Assert.Equal(3, anonymousResponse.OverallCount);
        var providers = anonymousResponse.StampingProviders.ToArray();
        Assert.Equal(["Other provider", "Touringen", "Unsupported link provider"], providers.Select(p => p.Name));

        var touringen = providers[1];
        Assert.Equal(StampingProvider.TouringenSlug, touringen.Slug);
        Assert.True(touringen.IsAnonymousAccessAllowed);
        Assert.Contains("430 offizielle Stempelstellen", touringen.Description, StringComparison.Ordinal);
        Assert.Equal("https://www.touringen.de/", touringen.WebsiteUrl);

        var other = providers[0];
        Assert.Equal("https://provider.example.test/info", other.WebsiteUrl);
        Assert.Null(providers[2].WebsiteUrl);

        Assert.NotNull(authenticatedResponse);
        Assert.Equal(4, authenticatedResponse.OverallCount);
        var harzerWandernadel = Assert.Single(authenticatedResponse.StampingProviders,
            provider => provider.Slug == StampingProvider.HarzerWandernadelSlug);
        Assert.Equal("HWN", harzerWandernadel.Abbreviation);
        Assert.False(harzerWandernadel.IsAnonymousAccessAllowed);
        Assert.Equal("https://www.harzer-wandernadel.de/", harzerWandernadel.WebsiteUrl);
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
        Assert.Contains("class=\"brand-logo\" aria-hidden=\"true\"", html, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("elements.providerInfoTrigger?.focus", script, StringComparison.Ordinal);
        Assert.Contains("url.protocol === \"http:\" || url.protocol === \"https:\"", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.provider?.name", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.provider?.abbreviation", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BundledFrontendSupportsAccessibleVisitCreationEditingAndConfirmedDeletion()
    {
        using var client = CreateClient(_factory);

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/toured.css");
        var script = await client.GetStringAsync("/js/toured.js");

        Assert.Contains("id=\"visitNowButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jetzt als besucht eintragen", html, StringComparison.Ordinal);
        Assert.Contains("id=\"openVisitFormButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"editVisitButton\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Datum und Uhrzeit bearbeiten", html, StringComparison.Ordinal);
        Assert.Contains("id=\"visitedOnInput\" name=\"visitedOn\" type=\"date\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"visitedAtInput\" name=\"visitedAt\" type=\"time\" disabled", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ohne Angaben wird nur gespeichert", html, StringComparison.Ordinal);
        Assert.Contains("id=\"deleteVisitDialog\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Besuch endgültig löschen", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\" aria-live=\"polite\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".visit-controls", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", css, StringComparison.Ordinal);
        Assert.Contains("saveVisit(\"PUT\"", script, StringComparison.Ordinal);
        Assert.Contains("?provider=${provider}", script, StringComparison.Ordinal);
        Assert.Contains("? \"PATCH\" : \"PUT\"", script, StringComparison.Ordinal);
        Assert.Contains("sendVisitRequest(\"DELETE\"", script, StringComparison.Ordinal);
        Assert.Contains("elements.deleteVisitDialog.showModal()", script, StringComparison.Ordinal);
        Assert.Contains("wirklich gelöscht werden", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.isVisited = isVisited", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.visitedOn = visitedOn", script, StringComparison.Ordinal);
        Assert.Contains("stampingPoint.visitedAt = visitedAt", script, StringComparison.Ordinal);
        Assert.Contains("elements.visitLoginLink.hidden", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivacyNoticeIsPublicLinkedAndExcludedFromSearchIndexing()
    {
        using var client = CreateClient(_factory);

        var frontendScript = await client.GetStringAsync("/js/toured.js");
        var privacyResponse = await client.GetAsync("/datenschutz/");
        var privacyNotice = await privacyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
        Assert.Contains("&copy; TourEd 2023 · <a class=\"privacy-link\" href=\"datenschutz/\">Datenschutz</a>", frontendScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"robots\" content=\"noindex, nofollow, noarchive\"", privacyNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tino@schuettpelz.org", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Die Funktion „Abmelden“ beendet nur die aktuelle Sitzung", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Eine Verbindung zu dieser Website wird erst hergestellt, wenn der Link bewusst geöffnet wird", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("TourEd übermittelt beim Öffnen eines Anbieterlinks weder die E-Mail-Adresse noch gespeicherte Stempelbesuche", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("TourEd ist ein unabhängiges Projekt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Beim bewussten Öffnen eines externen Anbieterlinks", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("ausschließlich für angemeldete, zuvor freigeschaltete TourEd-Benutzer sichtbar", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("keine Konto- oder Besuchsdaten an den Stempelanbieter übermittelt", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("Ein Besuch kann auch ohne Datum und Uhrzeit eingetragen werden", privacyNotice, StringComparison.Ordinal);
        Assert.Contains("den einzelnen Besuch nach einer Bestätigung vollständig löschen", privacyNotice, StringComparison.Ordinal);
        Assert.DoesNotContain("Google Hosted Libraries", privacyNotice, StringComparison.Ordinal);
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
            var providersResponse = await client.GetAsync("/toured/api/providers");
            var restrictedPointsResponse = await client.GetAsync("/toured/api/points?provider=harzer-wandernadel");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, styleResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, scriptResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, neutralPinResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, visitedPinResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, providersResponse.StatusCode);
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
