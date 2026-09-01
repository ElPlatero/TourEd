using Api.Repositories;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class DataRetentionCleanupServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void IntervalAndRetentionConstantsAreCorrect()
    {
        Assert.Equal(TimeSpan.FromHours(24), DataRetentionCleanupService.Interval);
        Assert.Equal(TimeSpan.FromDays(90), DataRetentionCleanupService.AdminAuditRetention);
    }

    [Fact]
    public async Task CleanupFailureIsContainedForScheduledRetry()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        await serviceProvider.DisposeAsync();
        var service = new DataRetentionCleanupService(
            scopeFactory,
            NullLogger<DataRetentionCleanupService>.Instance,
            TimeProvider.System);

        var succeeded = await service.RunCleanupSafelyAsync();

        Assert.False(succeeded);
        Assert.Equal(TimeSpan.FromHours(24), DataRetentionCleanupService.Interval);
    }

    [Fact]
    public async Task AuditEntriesOlderThanNinetyDaysAreDeletedWhileExactBoundaryAndFreshRemain()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var ninetyDaysAgo = now.AddDays(-90);

        var freshEntry = new AdminAuditEntry { CreatedAt = now.AddDays(-1), Action = "user.created", ActorUserId = 1 };
        var exactBoundaryEntry = new AdminAuditEntry { CreatedAt = ninetyDaysAgo, Action = "provider.granted", ActorUserId = 1 };
        var oneTickOlderEntry = new AdminAuditEntry { CreatedAt = ninetyDaysAgo.AddTicks(-1), Action = "provider.revoked", ActorUserId = 1 };
        var olderEntry = new AdminAuditEntry { CreatedAt = now.AddDays(-91), Action = "registration.approved", ActorUserId = 1 };

        context.AdminAuditEntries.AddRange(freshEntry, exactBoundaryEntry, oneTickOlderEntry, olderEntry);
        await context.SaveChangesAsync();

        var repository = new TouredRepository(context);
        var createdBefore = now - TimeSpan.FromDays(90);
        var deletedCount = await repository.CleanupExpiredAdminAuditEntriesAsync(createdBefore);

        Assert.Equal(2, deletedCount);

        var remainingIds = await context.AdminAuditEntries.AsNoTracking().Select(e => e.Id).ToListAsync();
        Assert.Contains(freshEntry.Id, remainingIds);
        Assert.Contains(exactBoundaryEntry.Id, remainingIds);
        Assert.DoesNotContain(oneTickOlderEntry.Id, remainingIds);
        Assert.DoesNotContain(olderEntry.Id, remainingIds);
    }

    [Fact]
    public async Task AllAuditActionsAreTreatedEquallyDuringCleanup()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiredTime = now.AddDays(-91);
        var freshTime = now.AddDays(-10);

        var actions = new[]
        {
            "registration.approved",
            "registration.rejected",
            "provider.granted",
            "provider.revoked",
            "default-provider.changed",
            "user.deleted"
        };

        foreach (var action in actions)
        {
            context.AdminAuditEntries.Add(new AdminAuditEntry
            {
                CreatedAt = expiredTime,
                Action = action,
                ActorUserId = 1,
                TargetUserId = 2,
                RegistrationRequestId = 3,
                ProviderSlug = "touringen"
            });
            context.AdminAuditEntries.Add(new AdminAuditEntry
            {
                CreatedAt = freshTime,
                Action = action,
                ActorUserId = 1,
                TargetUserId = 2,
                RegistrationRequestId = 3,
                ProviderSlug = "touringen"
            });
        }
        await context.SaveChangesAsync();

        var repository = new TouredRepository(context);
        var createdBefore = now - TimeSpan.FromDays(90);
        var deletedCount = await repository.CleanupExpiredAdminAuditEntriesAsync(createdBefore);

        Assert.Equal(actions.Length, deletedCount);

        var remaining = await context.AdminAuditEntries.AsNoTracking().ToListAsync();
        Assert.Equal(actions.Length, remaining.Count);
        Assert.All(remaining, entry => Assert.Equal(freshTime, entry.CreatedAt));
    }

    [Fact]
    public async Task ManualServiceRunCleansUpBothExpiredRegistrationRequestsAndExpiredAuditEntries()
    {
        await using (var context = await CreateInitializedContextAsync())
        {
            var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

            // Registration requests: 30 days retention
            context.RegistrationRequests.AddRange(
                new RegistrationRequest
                {
                    GoogleSubject = "expired-pending",
                    Email = "expired-pending@example.test",
                    Status = RegistrationRequestStatus.Pending,
                    CreatedAt = now.AddDays(-31)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "fresh-pending",
                    Email = "fresh-pending@example.test",
                    Status = RegistrationRequestStatus.Pending,
                    CreatedAt = now.AddDays(-10)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "expired-approved",
                    Email = "expired-approved@example.test",
                    Status = RegistrationRequestStatus.Approved,
                    CreatedAt = now.AddDays(-40),
                    DecidedAt = now.AddDays(-31)
                },
                new RegistrationRequest
                {
                    GoogleSubject = "fresh-approved",
                    Email = "fresh-approved@example.test",
                    Status = RegistrationRequestStatus.Approved,
                    CreatedAt = now.AddDays(-40),
                    DecidedAt = now.AddDays(-10)
                });

            // Admin audit entries: 90 days retention
            context.AdminAuditEntries.AddRange(
                new AdminAuditEntry
                {
                    CreatedAt = now.AddDays(-95),
                    Action = "registration.approved",
                    ActorUserId = 1
                },
                new AdminAuditEntry
                {
                    CreatedAt = now.AddDays(-15),
                    Action = "registration.approved",
                    ActorUserId = 1
                });

            await context.SaveChangesAsync();
        }

        var testNow = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(testNow);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDbContext<DataContext>();
        services.AddScoped<TouredRepository>();
        services.AddSingleton<DataRetentionCleanupService>();
        await using var serviceProvider = services.BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<DataRetentionCleanupService>();
        var result = await service.RunCleanupSafelyAsync();

        Assert.True(result);

        await using (var verifyContext = CreateContext())
        {
            var requests = await verifyContext.RegistrationRequests.AsNoTracking().ToListAsync();
            Assert.Equal(2, requests.Count);
            Assert.Contains(requests, r => r.GoogleSubject == "fresh-pending");
            Assert.Contains(requests, r => r.GoogleSubject == "fresh-approved");
            Assert.DoesNotContain(requests, r => r.GoogleSubject == "expired-pending");
            Assert.DoesNotContain(requests, r => r.GoogleSubject == "expired-approved");

            var auditEntries = await verifyContext.AdminAuditEntries.AsNoTracking().ToListAsync();
            var singleAudit = Assert.Single(auditEntries);
            Assert.Equal(testNow.UtcDateTime.AddDays(-15), singleAudit.CreatedAt);
        }
    }

    [Fact]
    public async Task CleanupReturnsFalseWhenCancelled()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new DataRetentionCleanupService(
            scopeFactory,
            NullLogger<DataRetentionCleanupService>.Instance,
            TimeProvider.System);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.RunCleanupSafelyAsync(cts.Token);
        Assert.False(result);
    }

    [Fact]
    public async Task RegistrationCleanupFailureDoesNotPreventAuditCleanup()
    {
        await using (var context = await CreateInitializedContextAsync())
        {
            var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            context.AdminAuditEntries.Add(new AdminAuditEntry
            {
                CreatedAt = now.AddDays(-95),
                Action = "registration.approved",
                ActorUserId = 1
            });
            await context.SaveChangesAsync();
        }

        var testNow = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(testNow);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDbContext<DataContext>();
        services.AddScoped<TouredRepository>();
        await using var innerProvider = services.BuildServiceProvider();

        // Fail only the first scope (registration cleanup)
        var failingScopeFactory = new ScopeCountingScopeFactory(innerProvider.GetRequiredService<IServiceScopeFactory>(), failOnScopeNumber: 1);
        var service = new DataRetentionCleanupService(
            failingScopeFactory,
            NullLogger<DataRetentionCleanupService>.Instance,
            timeProvider);

        var result = await service.RunCleanupSafelyAsync();

        Assert.False(result);

        await using (var verifyContext = CreateContext())
        {
            var auditEntries = await verifyContext.AdminAuditEntries.AsNoTracking().ToListAsync();
            Assert.Empty(auditEntries);
        }
    }

    private sealed class ScopeCountingScopeFactory(IServiceScopeFactory innerFactory, int failOnScopeNumber) : IServiceScopeFactory
    {
        private int _scopeCount;

        public IServiceScope CreateScope()
        {
            var count = Interlocked.Increment(ref _scopeCount);
            if (count == failOnScopeNumber)
            {
                throw new InvalidOperationException($"Simulated scope failure on scope {count}");
            }
            return innerFactory.CreateScope();
        }
    }

    private DataContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath}"
            })
            .Build();
        return new DataContext(configuration);
    }

    private async Task<DataContext> CreateInitializedContextAsync()
    {
        var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
