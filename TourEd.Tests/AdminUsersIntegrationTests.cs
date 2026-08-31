using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Dto;
using Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class AdminUsersIntegrationTests : IAsyncLifetime
{
    private const string CliToken = "test-only-admin-users-token-0123456789abcdef0123456789abcdef";
    private const string AdminEmail = "admin-users@example.test";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-admin-users-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-admin-users-keys-{Guid.NewGuid():N}");
    private AdminWebApplicationFactory _factory = null!;
    private int _adminUserId;
    private int _targetUserId;

    public async Task InitializeAsync()
    {
        _factory = new AdminWebApplicationFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();

        var admin = new User { Email = AdminEmail };
        var target = new User
        {
            Email = "hiker@example.test",
            GoogleSubject = "google-subject",
            DefaultStampingProviderId = StampingProvider.TouringenId
        };
        context.Users.AddRange(admin, target);
        await context.SaveChangesAsync();
        context.UserStampingProviders.Add(new UserStampingProvider
        {
            UserId = target.Id,
            StampingProviderId = StampingProvider.TouringenId
        });
        await context.SaveChangesAsync();
        _adminUserId = admin.Id;
        _targetUserId = target.Id;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        if (Directory.Exists(_keysPath)) Directory.Delete(_keysPath, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdminEndpointsRequireCliToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/providers",
            new UpdateAdminUserProvidersRequestDto([], null))).StatusCode);
    }

    [Fact]
    public async Task ListsUsersAndAllProviders()
    {
        using var client = CreateAuthorizedClient();

        var users = await client.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users");
        var providers = await client.GetFromJsonAsync<List<AdminProviderDto>>("/api/admin/providers");

        Assert.NotNull(users);
        var target = Assert.Single(users, user => user.Id == _targetUserId);
        Assert.True(target.IsGoogleLinked);
        Assert.Equal("touringen", target.DefaultProvider);
        Assert.Equal(["touringen"], target.Providers);

        Assert.NotNull(providers);
        Assert.Contains(providers, provider => provider.Slug == "touringen");
        Assert.Contains(providers, provider => provider.Slug == "harzer-wandernadel");
        Assert.True(providers.Count >= 6);
    }

    [Fact]
    public async Task ReplacesEntitlementsAndWritesMinimalAuditLog()
    {
        int visitedPointId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<DataContext>();
            visitedPointId = await setupContext.StampingPoints.Select(point => point.Id).FirstAsync();
            setupContext.UserVisits.Add(new UserVisit
            {
                UserId = _targetUserId,
                StampingPointId = visitedPointId,
                EntryCreated = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        using var client = CreateAuthorizedClient();
        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/providers",
            new UpdateAdminUserProvidersRequestDto(["harzer-wandernadel"], "harzer-wandernadel"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AdminUserDto>();
        Assert.NotNull(user);
        Assert.Equal("harzer-wandernadel", user.DefaultProvider);
        Assert.Equal(["harzer-wandernadel"], user.Providers);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var audit = await context.AdminAuditEntries.OrderBy(entry => entry.Id).ToListAsync();
        Assert.Equal(3, audit.Count);
        Assert.All(audit, entry =>
        {
            Assert.Equal(_adminUserId, entry.ActorUserId);
            Assert.Equal(_targetUserId, entry.TargetUserId);
        });
        Assert.Contains(audit, entry => entry.Action == "provider.revoked" && entry.ProviderSlug == "touringen");
        Assert.Contains(audit, entry => entry.Action == "provider.granted" && entry.ProviderSlug == "harzer-wandernadel");
        Assert.Contains(audit, entry => entry.Action == "default-provider.changed" && entry.ProviderSlug == "harzer-wandernadel");
        Assert.True(await context.UserVisits.AnyAsync(visit =>
            visit.UserId == _targetUserId && visit.StampingPointId == visitedPointId));
    }

    [Fact]
    public async Task RejectsUnknownDuplicateOrUnentitledDefaultProvider()
    {
        using var client = CreateAuthorizedClient();

        var unknown = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/providers",
            new UpdateAdminUserProvidersRequestDto(["unknown"], null));
        var duplicate = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/providers",
            new UpdateAdminUserProvidersRequestDto(["touringen", "TOURINGEN"], "touringen"));
        var invalidDefault = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/providers",
            new UpdateAdminUserProvidersRequestDto([], "touringen"));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDefault.StatusCode);
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CliToken);
        return client;
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
                    ["Authentication:Cli:UserEmail"] = AdminEmail,
                    ["DataProtection:KeysPath"] = keysPath
                }));
        }
    }
}
