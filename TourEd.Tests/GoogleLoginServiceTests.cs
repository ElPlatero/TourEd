using Api.Repositories;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class GoogleLoginServiceTests : IDisposable
{
    private const string PreviousMigration = "20260626010000_AddProviderFieldsToStampingPoints";
    private const string GoogleSubjectMigration = "20260828181042_AddGoogleSubjectToUsers";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-google-login-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GoogleSubjectMigrationPreservesExistingUserProviderAndVisit()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(PreviousMigration);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email, DefaultStampingProviderId) VALUES (7, 'existing@example.test', 1);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO StampingPoints (Id, Name, Longitude, Latitude, Number, Code, ProviderId, ExternalId) " +
            "VALUES (42, 'Existing point', '11.5', '50.5', 123, 456, 1, 'touringen-42');");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserVisit (Id, UserId, Visited, StampingPointId) " +
            "VALUES (100, 7, '2026-08-28 10:00:00', 42);");

        await context.Database.MigrateAsync();

        var migrations = await context.Database.GetAppliedMigrationsAsync();
        var user = await context.Users.AsNoTracking().SingleAsync();
        var visit = await context.UserVisits.AsNoTracking().SingleAsync();
        Assert.Contains(GoogleSubjectMigration, migrations);
        Assert.Equal(7, user.Id);
        Assert.Equal("existing@example.test", user.Email);
        Assert.Equal(1, user.DefaultStampingProviderId);
        Assert.Null(user.GoogleSubject);
        Assert.Equal(100, visit.Id);
        Assert.Equal(7, visit.UserId);
        Assert.Equal(42, visit.StampingPointId);
        Assert.Equal(new DateTime(2026, 8, 28, 10, 0, 0), visit.Visited);
    }

    [Fact]
    public async Task FirstLoginBindsKnownUnboundUser()
    {
        await using var context = await CreateInitializedContextAsync();
        var existingUser = await AddUserAsync(context, "known@example.test");
        var service = CreateService(context);

        var authenticatedUser = await service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "known@example.test", true));

        Assert.Equal(existingUser.Id, authenticatedUser.Id);
        Assert.Equal("google-subject-1", authenticatedUser.GoogleSubject);
        Assert.Equal(
            "google-subject-1",
            (await context.Users.AsNoTracking().SingleAsync(user => user.Id == existingUser.Id)).GoogleSubject);
    }

    [Fact]
    public async Task ValidGoogleClaimsCreateOnlyInternalTouredClaims()
    {
        await using var context = await CreateInitializedContextAsync();
        var existingUser = await AddUserAsync(context, "known@example.test");
        var service = CreateService(context);

        var principal = await service.CreatePrincipalAsync(
            new GoogleLoginClaims("google-subject-1", "known@example.test", true));

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal(
            new[]
            {
                new Claim(Constants.ClaimsNames.UserEmail, existingUser.Email),
                new Claim(Constants.ClaimsNames.UserId, existingUser.Id.ToString())
            }.Select(claim => (claim.Type, claim.Value)).OrderBy(claim => claim.Type),
            principal.Claims.Select(claim => (claim.Type, claim.Value)).OrderBy(claim => claim.Type));
    }

    [Fact]
    public async Task MissingSubjectIsRejected()
    {
        await using var context = await CreateInitializedContextAsync();
        await AddUserAsync(context, "known@example.test");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.CreatePrincipalAsync(
            new GoogleLoginClaims(string.Empty, "known@example.test", true)));

        Assert.Equal(GoogleLoginRejectionReason.InvalidClaims, exception.Reason);
    }

    [Fact]
    public async Task UnverifiedEmailIsRejected()
    {
        await using var context = await CreateInitializedContextAsync();
        await AddUserAsync(context, "known@example.test");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.CreatePrincipalAsync(
            new GoogleLoginClaims("google-subject-1", "known@example.test", false)));

        Assert.Equal(GoogleLoginRejectionReason.EmailNotVerified, exception.Reason);
        Assert.Null((await context.Users.AsNoTracking().SingleAsync()).GoogleSubject);
    }

    [Fact]
    public async Task RepeatedLoginFindsSameUserBySubject()
    {
        await using var context = await CreateInitializedContextAsync();
        var existingUser = await AddUserAsync(context, "old-address@example.test", "google-subject-1");
        var service = CreateService(context);

        var authenticatedUser = await service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "new-address@example.test", true));

        Assert.Equal(existingUser.Id, authenticatedUser.Id);
    }

    [Fact]
    public async Task FirstLoginMatchesEmailCaseInsensitively()
    {
        await using var context = await CreateInitializedContextAsync();
        var existingUser = await AddUserAsync(context, "Known.User@Example.Test");
        var service = CreateService(context);

        var authenticatedUser = await service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "  known.user@example.test  ", true));

        Assert.Equal(existingUser.Id, authenticatedUser.Id);
        Assert.Equal("google-subject-1", authenticatedUser.GoogleSubject);
    }

    [Fact]
    public async Task UnknownEmailCreatesPendingRegistrationRequestWithoutCreatingUser()
    {
        await using var context = await CreateInitializedContextAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "unknown@example.test", true)));

        Assert.Equal(GoogleLoginRejectionReason.RegistrationPending, exception.Reason);
        Assert.Empty(await context.Users.AsNoTracking().ToListAsync());

        var requests = await context.RegistrationRequests.AsNoTracking().ToListAsync();
        var request = Assert.Single(requests);
        Assert.Equal("google-subject-1", request.GoogleSubject);
        Assert.Equal("unknown@example.test", request.Email);
        Assert.Equal(RegistrationRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task RepeatedLoginWithPendingRequestDoesNotDuplicateAndUpdatesChangedEmail()
    {
        await using var context = await CreateInitializedContextAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "initial@example.test", true)));

        var initialRequest = await context.RegistrationRequests.AsNoTracking().SingleAsync();
        var initialCreatedAt = initialRequest.CreatedAt;

        await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "updated-address@example.test", true)));

        var requests = await context.RegistrationRequests.AsNoTracking().ToListAsync();
        var updatedRequest = Assert.Single(requests);
        Assert.Equal(initialRequest.Id, updatedRequest.Id);
        Assert.Equal("google-subject-1", updatedRequest.GoogleSubject);
        Assert.Equal("updated-address@example.test", updatedRequest.Email);
        Assert.Equal(RegistrationRequestStatus.Pending, updatedRequest.Status);
        Assert.Equal(initialCreatedAt, updatedRequest.CreatedAt);
        Assert.NotNull(updatedRequest.UpdatedAt);
    }

    [Fact]
    public async Task RepeatedLoginWithRejectedRequestReAppliesAsPending()
    {
        await using var context = await CreateInitializedContextAsync();
        var service = CreateService(context);
        var repository = new TouredRepository(context);

        await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "applicant@example.test", true)));

        var initialRequest = await context.RegistrationRequests.SingleAsync();
        await repository.RejectRegistrationRequestAsync(initialRequest.Id, actorUserId: 1);

        var rejectedRequest = await context.RegistrationRequests.AsNoTracking().SingleAsync();
        Assert.Equal(RegistrationRequestStatus.Rejected, rejectedRequest.Status);
        Assert.NotNull(rejectedRequest.DecidedAt);

        await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("google-subject-1", "applicant@example.test", true)));

        var reAppliedRequest = await context.RegistrationRequests.AsNoTracking().SingleAsync();
        Assert.Equal(RegistrationRequestStatus.Pending, reAppliedRequest.Status);
        Assert.Null(reAppliedRequest.DecidedAt);
    }

    [Fact]
    public async Task ProcessLoginReturnsPendingResultForNewRegistration()
    {
        await using var context = await CreateInitializedContextAsync();
        var service = CreateService(context);

        var result = await service.ProcessLoginAsync(
            new GoogleLoginClaims("google-subject-99", "new-hiker@example.test", true));

        Assert.Equal(GoogleLoginStatus.Pending, result.Status);
        Assert.Null(result.User);
        Assert.Null(result.Principal);
        Assert.NotNull(result.RegistrationRequest);
        Assert.Equal("google-subject-99", result.RegistrationRequest.GoogleSubject);
    }

    [Fact]
    public async Task SubjectAlreadyUsedByAnotherUserCannotBeBound()
    {
        await using var context = await CreateInitializedContextAsync();
        await AddUserAsync(context, "first@example.test", "used-subject");
        var secondUser = await AddUserAsync(context, "second@example.test");
        var repository = new TouredRepository(context);

        var wasBound = await repository.TryBindGoogleSubjectAsync(secondUser.Id, "used-subject");

        Assert.False(wasBound);
        Assert.Null((await context.Users.AsNoTracking().SingleAsync(user => user.Id == secondUser.Id)).GoogleSubject);
    }

    [Fact]
    public async Task DifferentSubjectForAlreadyBoundUserIsRejected()
    {
        await using var context = await CreateInitializedContextAsync();
        await AddUserAsync(context, "known@example.test", "original-subject");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<GoogleLoginRejectedException>(() => service.AuthenticateAsync(
            new GoogleLoginClaims("different-subject", "known@example.test", true)));

        Assert.Equal(GoogleLoginRejectionReason.UserAlreadyBound, exception.Reason);
        Assert.Equal("original-subject", (await context.Users.AsNoTracking().SingleAsync()).GoogleSubject);
    }

    [Fact]
    public async Task DatabaseEnforcesUniqueGoogleSubject()
    {
        await using var context = await CreateInitializedContextAsync();
        context.Users.AddRange(
            new User { Email = "first@example.test", GoogleSubject = "duplicate-subject" },
            new User { Email = "second@example.test", GoogleSubject = "duplicate-subject" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
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

    private static async Task<User> AddUserAsync(DataContext context, string email, string? googleSubject = null)
    {
        var user = new User { Email = email, GoogleSubject = googleSubject };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static GoogleLoginService CreateService(DataContext context)
        => new(new TouredRepository(context));

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
