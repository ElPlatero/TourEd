using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Dto;
using Api.Repositories;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class AdminRegistrationsIntegrationTests : IAsyncLifetime
{
    private const string CliToken = "test-only-admin-registrations-token-0123456789abcdef";
    private const string AdminEmail = "admin-registrations@example.test";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-admin-registrations-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-admin-registrations-keys-{Guid.NewGuid():N}");
    private AdminWebApplicationFactory _factory = null!;
    private int _adminUserId;

    public async Task InitializeAsync()
    {
        _factory = new AdminWebApplicationFactory(_databasePath, _keysPath);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();

        var admin = new User { Email = AdminEmail };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        _adminUserId = admin.Id;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        if (Directory.Exists(_keysPath)) Directory.Delete(_keysPath, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdminRegistrationsEndpointsRequireCliToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/registrations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/admin/registrations/1/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/admin/registrations/1/reject", null)).StatusCode);
    }

    [Fact]
    public async Task ListsRegistrationsWithFilter()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            context.RegistrationRequests.AddRange(
                new RegistrationRequest
                {
                    GoogleSubject = "sub-pending-1",
                    Email = "pending1@example.test",
                    Status = RegistrationRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "sub-rejected-1",
                    Email = "rejected1@example.test",
                    Status = RegistrationRequestStatus.Rejected,
                    CreatedAt = DateTime.UtcNow.AddHours(-4),
                    DecidedAt = DateTime.UtcNow.AddHours(-1)
                });
            await context.SaveChangesAsync();
        }

        using var client = CreateAuthorizedClient();

        var allRequests = await client.GetFromJsonAsync<List<AdminRegistrationRequestDto>>("/api/admin/registrations");
        var pendingRequests = await client.GetFromJsonAsync<List<AdminRegistrationRequestDto>>("/api/admin/registrations?status=pending");

        Assert.NotNull(allRequests);
        Assert.Equal(2, allRequests.Count);

        Assert.NotNull(pendingRequests);
        var pending = Assert.Single(pendingRequests);
        Assert.Equal("pending1@example.test", pending.Email);
        Assert.Equal("pending", pending.Status);
    }

    [Fact]
    public async Task ApprovingRegistrationCreatesUserWithoutEntitlementsAndLogsAudit()
    {
        int requestId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var req = new RegistrationRequest
            {
                GoogleSubject = "sub-applicant-42",
                Email = "applicant42@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            context.RegistrationRequests.Add(req);
            await context.SaveChangesAsync();
            requestId = req.Id;
        }

        using var client = CreateAuthorizedClient();
        var response = await client.PostAsync($"/api/admin/registrations/{requestId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdminRegistrationRequestDto>();
        Assert.NotNull(result);
        Assert.Equal("approved", result.Status);
        Assert.Equal("applicant42@example.test", result.Email);
        Assert.NotNull(result.DecidedAt);

        await using (var verifyScope = _factory.Services.CreateAsyncScope())
        {
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
            var createdUser = await verifyContext.Users
                .Include(u => u.StampingProviders)
                .SingleOrDefaultAsync(u => u.Email == "applicant42@example.test");

            Assert.NotNull(createdUser);
            Assert.Equal("sub-applicant-42", createdUser.GoogleSubject);
            Assert.Null(createdUser.DefaultStampingProviderId);
            Assert.Empty(createdUser.StampingProviders);

            var auditEntry = await verifyContext.AdminAuditEntries
                .SingleOrDefaultAsync(a => a.Action == "registration.approved" && a.TargetUserId == createdUser.Id);
            Assert.NotNull(auditEntry);
            Assert.Equal(_adminUserId, auditEntry.ActorUserId);
            Assert.Null(auditEntry.ProviderSlug);
            Assert.Equal(requestId, auditEntry.RegistrationRequestId);
        }
    }

    [Fact]
    public async Task RejectingRegistrationUpdatesStatusAndLogsAudit()
    {
        int requestId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var req = new RegistrationRequest
            {
                GoogleSubject = "sub-rejected-99",
                Email = "unwanted@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            context.RegistrationRequests.Add(req);
            await context.SaveChangesAsync();
            requestId = req.Id;
        }

        using var client = CreateAuthorizedClient();
        var response = await client.PostAsync($"/api/admin/registrations/{requestId}/reject", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AdminRegistrationRequestDto>();
        Assert.NotNull(result);
        Assert.Equal("rejected", result.Status);
        Assert.NotNull(result.DecidedAt);

        await using (var verifyScope = _factory.Services.CreateAsyncScope())
        {
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
            var user = await verifyContext.Users.FirstOrDefaultAsync(u => u.Email == "unwanted@example.test");
            Assert.Null(user);

            var auditEntry = await verifyContext.AdminAuditEntries
                .SingleOrDefaultAsync(a => a.Action == "registration.rejected");
            Assert.NotNull(auditEntry);
            Assert.Equal(_adminUserId, auditEntry.ActorUserId);
            Assert.Null(auditEntry.TargetUserId);
            Assert.Equal(requestId, auditEntry.RegistrationRequestId);
        }

        var auditResponse = await client.GetFromJsonAsync<List<AdminAuditEntryDto>>(
            "/api/admin/audit?offset=0&limit=250");
        Assert.NotNull(auditResponse);
        var auditDto = Assert.Single(auditResponse, entry => entry.RegistrationRequestId == requestId);
        Assert.Equal("registration.rejected", auditDto.Action);
        Assert.Equal(_adminUserId, auditDto.ActorUserId);
        Assert.Null(auditDto.TargetUserId);
    }

    [Theory]
    [InlineData("approve", "approve")]
    [InlineData("approve", "reject")]
    [InlineData("reject", "approve")]
    [InlineData("reject", "reject")]
    public async Task RegistrationRequestCanOnlyBeDecidedOnce(string firstDecision, string secondDecision)
    {
        int requestId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var request = new RegistrationRequest
            {
                GoogleSubject = $"single-decision-{firstDecision}-{secondDecision}",
                Email = $"{firstDecision}-{secondDecision}@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            context.RegistrationRequests.Add(request);
            await context.SaveChangesAsync();
            requestId = request.Id;
        }

        using var client = CreateAuthorizedClient();
        var firstResponse = await client.PostAsync($"/api/admin/registrations/{requestId}/{firstDecision}", null);
        int auditCountAfterFirstDecision;
        await using (var auditScope = _factory.Services.CreateAsyncScope())
        {
            auditCountAfterFirstDecision = await auditScope.ServiceProvider
                .GetRequiredService<DataContext>()
                .AdminAuditEntries.CountAsync();
        }
        var secondResponse = await client.PostAsync($"/api/admin/registrations/{requestId}/{secondDecision}", null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var requestAfterDecisions = await verifyContext.RegistrationRequests.SingleAsync(r => r.Id == requestId);
        var expectedStatus = firstDecision == "approve"
            ? RegistrationRequestStatus.Approved
            : RegistrationRequestStatus.Rejected;
        Assert.Equal(expectedStatus, requestAfterDecisions.Status);
        Assert.Equal(auditCountAfterFirstDecision, await verifyContext.AdminAuditEntries.CountAsync());
    }

    [Fact]
    public async Task ExpiredRegistrationRequestsArePurgedAfter30Days()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            context.RegistrationRequests.AddRange(
                new RegistrationRequest
                {
                    GoogleSubject = "old-pending",
                    Email = "old-pending@example.test",
                    Status = RegistrationRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddDays(-31)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "fresh-pending",
                    Email = "fresh-pending@example.test",
                    Status = RegistrationRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "old-rejected",
                    Email = "old-rejected@example.test",
                    Status = RegistrationRequestStatus.Rejected,
                    CreatedAt = DateTime.UtcNow.AddDays(-40),
                    DecidedAt = DateTime.UtcNow.AddDays(-31)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "old-approved",
                    Email = "old-approved@example.test",
                    Status = RegistrationRequestStatus.Approved,
                    CreatedAt = DateTime.UtcNow.AddDays(-40),
                    DecidedAt = DateTime.UtcNow.AddDays(-31)
                });
            await context.SaveChangesAsync();
        }

        var cleanupService = _factory.Services.GetRequiredService<DataRetentionCleanupService>();
        Assert.True(await cleanupService.RunCleanupSafelyAsync());

        using var client = CreateAuthorizedClient();
        var list = await client.GetFromJsonAsync<List<AdminRegistrationRequestDto>>("/api/admin/registrations");

        Assert.NotNull(list);
        var single = Assert.Single(list);
        Assert.Equal("fresh-pending@example.test", single.Email);
    }

    [Fact]
    public async Task CleanupUsesStrictThirtyDayBoundaryForEveryStatus()
    {
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        context.RegistrationRequests.AddRange(
            new RegistrationRequest
            {
                GoogleSubject = "boundary-pending",
                Email = "boundary-pending@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddDays(-30)
            },
            new RegistrationRequest
            {
                GoogleSubject = "boundary-rejected",
                Email = "boundary-rejected@example.test",
                Status = RegistrationRequestStatus.Rejected,
                CreatedAt = now.AddDays(-40),
                DecidedAt = now.AddDays(-30)
            },
            new RegistrationRequest
            {
                GoogleSubject = "expired-approved",
                Email = "expired-approved@example.test",
                Status = RegistrationRequestStatus.Approved,
                CreatedAt = now.AddDays(-40),
                DecidedAt = now.AddDays(-30).AddTicks(-1)
            });
        await context.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<TouredRepository>();
        await repository.CleanupExpiredRegistrationRequestsAsync(now);

        var subjects = await context.RegistrationRequests
            .AsNoTracking()
            .Select(request => request.GoogleSubject)
            .ToListAsync();
        Assert.Contains("boundary-pending", subjects);
        Assert.Contains("boundary-rejected", subjects);
        Assert.DoesNotContain("expired-approved", subjects);
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
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TouredDb"] = $"Data Source={databasePath}",
                    ["Authentication:Google:ClientId"] = "test-client-id",
                    ["Authentication:Google:ClientSecret"] = "test-client-secret",
                    ["DataProtection:KeysPath"] = keysPath,
                    ["Authentication:Cli:UserEmail"] = AdminEmail,
                    ["Authentication:Cli:Token"] = CliToken
                });
            });
        }
    }
}
