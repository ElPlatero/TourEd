using System.Reflection;
using Api.Options;
using Api.Repositories;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class RegistrationRequestNotificationTests : IDisposable
{
    private const string PreviousMigration = "20260901043735_ExtendAdminAuditEntries";
    private const string CurrentMigration = "20260901131227_AddRegistrationRequestAdminNotification";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-notif-tests-{Guid.NewGuid():N}.db");

    #region Configuration Tests

    [Fact]
    public void DisabledConfigurationRequiresNoSmtpValues()
    {
        var options = new RegistrationNotificationOptions
        {
            Enabled = false,
            SmtpHost = string.Empty,
            SmtpPassword = string.Empty
        };

        var isValid = options.Validate(out var errors);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void EnabledConfigurationWithMissingHostIsRejected()
    {
        var options = new RegistrationNotificationOptions
        {
            Enabled = true,
            SmtpHost = "   ",
            SmtpPort = 587,
            SmtpUsername = "user@example.test",
            SmtpPassword = "secret-password",
            SenderAddress = "sender@example.test",
            RecipientAddress = "admin@example.test"
        };

        var isValid = options.Validate(out var errors);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("SmtpHost"));
    }

    [Fact]
    public void EnabledConfigurationWithEmptyPasswordIsRejected()
    {
        var options = new RegistrationNotificationOptions
        {
            Enabled = true,
            SmtpHost = "smtp.ionos.de",
            SmtpPort = 587,
            SmtpUsername = "user@example.test",
            SmtpPassword = "",
            SenderAddress = "sender@example.test",
            RecipientAddress = "admin@example.test"
        };

        var isValid = options.Validate(out var errors);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("SmtpPassword"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void InvalidPortIsRejected(int invalidPort)
    {
        var options = new RegistrationNotificationOptions
        {
            Enabled = true,
            SmtpHost = "smtp.ionos.de",
            SmtpPort = invalidPort,
            SmtpUsername = "user@example.test",
            SmtpPassword = "secret-password",
            SenderAddress = "sender@example.test",
            RecipientAddress = "admin@example.test"
        };

        var isValid = options.Validate(out var errors);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("SmtpPort"));
    }

    [Theory]
    [InlineData("not-an-email", "admin@example.test", "SenderAddress")]
    [InlineData("sender@example.test", "not-an-email", "RecipientAddress")]
    [InlineData("", "admin@example.test", "SenderAddress")]
    public void InvalidSenderOrRecipientAddressIsRejected(string sender, string recipient, string errorField)
    {
        var options = new RegistrationNotificationOptions
        {
            Enabled = true,
            SmtpHost = "smtp.ionos.de",
            SmtpPort = 587,
            SmtpUsername = "user@example.test",
            SmtpPassword = "secret-password",
            SenderAddress = sender,
            RecipientAddress = recipient
        };

        var isValid = options.Validate(out var errors);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains(errorField));
    }

    [Fact]
    public void ValidationErrorsNeverContainPassword()
    {
        const string secretPassword = "SuperConfidentialPassword123!";
        var options = new RegistrationNotificationOptions
        {
            Enabled = true,
            SmtpHost = "",
            SmtpPort = 99999,
            SmtpUsername = "",
            SmtpPassword = secretPassword,
            SenderAddress = "invalid-address",
            RecipientAddress = "invalid-address"
        };

        var isValid = options.Validate(out var errors);

        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.DoesNotContain(secretPassword, error));
    }

    #endregion

    #region Notification Service Tests

    [Fact]
    public void IntervalAndCooldownConstantsAreCorrect()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), RegistrationRequestNotificationService.PollInterval);
        Assert.Equal(TimeSpan.FromHours(1), RegistrationRequestNotificationService.NotificationCooldown);
    }

    [Fact]
    public async Task NoPendingRequestsSendsNoNotification()
    {
        await using var context = await CreateInitializedContextAsync();
        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions());

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        Assert.Empty(fakeSender.SentMessages);
    }

    [Fact]
    public async Task DisabledConfigurationSendsNoNotification()
    {
        await using var context = await CreateInitializedContextAsync();
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var options = CreateValidOptions();
        options.Enabled = false;
        var service = CreateService(context, fakeSender, options);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        Assert.Empty(fakeSender.SentMessages);
    }

    [Fact]
    public async Task SingleNewPendingRequestSendsExactlyOneNotification()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        var request = new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        };
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        var message = Assert.Single(fakeSender.SentMessages);
        Assert.Equal(1, message.NewRequestCount);
        Assert.Equal(1, message.TotalPendingRequestCount);

        var updated = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(now, updated.AdminNotificationSentAt);
    }

    [Fact]
    public async Task MultipleNewPendingRequestsSendSingleAggregatedNotification()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        context.RegistrationRequests.AddRange(
            new RegistrationRequest
            {
                GoogleSubject = "sub-1",
                Email = "user1@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddMinutes(-10)
            },
            new RegistrationRequest
            {
                GoogleSubject = "sub-2",
                Email = "user2@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddMinutes(-5)
            },
            new RegistrationRequest
            {
                GoogleSubject = "sub-3",
                Email = "user3@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now
            });
        await SetLastSentAtAsync(context, now.AddHours(-2));
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        var message = Assert.Single(fakeSender.SentMessages);
        Assert.Equal(3, message.NewRequestCount);
        Assert.Equal(3, message.TotalPendingRequestCount);

        var allRequests = await context.RegistrationRequests.AsNoTracking().ToListAsync();
        Assert.All(allRequests, r => Assert.Equal(now, r.AdminNotificationSentAt));
    }

    [Fact]
    public async Task NewAndTotalCountsAreCalculatedCorrectly()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        // 2 already notified pending requests (notified > 1 hour ago)
        context.RegistrationRequests.AddRange(
            new RegistrationRequest
            {
                GoogleSubject = "sub-old-1",
                Email = "old1@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddHours(-3),
                AdminNotificationSentAt = now.AddHours(-2)
            },
            new RegistrationRequest
            {
                GoogleSubject = "sub-old-2",
                Email = "old2@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddHours(-3),
                AdminNotificationSentAt = now.AddHours(-2)
            },
            // 1 new unnotified pending request
            new RegistrationRequest
            {
                GoogleSubject = "sub-new-1",
                Email = "new1@example.test",
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = now.AddMinutes(-10),
                AdminNotificationSentAt = null
            });
        await SetLastSentAtAsync(context, now.AddHours(-2));
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        var message = Assert.Single(fakeSender.SentMessages);
        Assert.Equal(1, message.NewRequestCount);
        Assert.Equal(3, message.TotalPendingRequestCount);
    }

    [Fact]
    public async Task SuccessfullyNotifiedRequestsAreMarkedWithTimestamp()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        var req = new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        };
        context.RegistrationRequests.Add(req);
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        await service.RunNotificationSafelyAsync();

        var reloaded = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == req.Id);
        Assert.Equal(now, reloaded.AdminNotificationSentAt);
        Assert.Equal(
            now,
            await context.RegistrationNotificationStates
                .Where(state => state.Id == RegistrationNotificationState.SingletonId)
                .Select(state => state.LastSentAt)
                .SingleAsync());
    }

    [Fact]
    public async Task SecondRunDoesNotResendAlreadyNotifiedRequests()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        // Run 1
        var run1 = await service.RunNotificationSafelyAsync();
        Assert.True(run1);
        Assert.Single(fakeSender.SentMessages);

        // Run 2 (5 minutes later)
        timeProvider.SetUtcNow(now.AddMinutes(5));
        var run2 = await service.RunNotificationSafelyAsync();
        Assert.True(run2);
        Assert.Single(fakeSender.SentMessages); // Still 1, no second mail sent
    }

    [Fact]
    public async Task NewRequestWithinCooldownPeriodRemainsUnmarkedAndSendsNoNotification()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        // A notification was sent 30 minutes ago
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-40),
            AdminNotificationSentAt = now.AddMinutes(-30)
        });

        // A new request arrived 10 minutes ago
        var newRequest = new RegistrationRequest
        {
            GoogleSubject = "sub-2",
            Email = "user2@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-10),
            AdminNotificationSentAt = null
        };
        context.RegistrationRequests.Add(newRequest);
        await SetLastSentAtAsync(context, now.AddMinutes(-30));
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        Assert.Empty(fakeSender.SentMessages); // No mail sent during cooldown

        // New request remains unnotified for subsequent runs
        var pendingNew = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == newRequest.Id);
        Assert.Null(pendingNew.AdminNotificationSentAt);
    }

    [Fact]
    public async Task RequestIsSentAfterCooldownExpires()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        // A notification was sent 65 minutes ago (> 1 hour cooldown)
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-70),
            AdminNotificationSentAt = now.AddMinutes(-65)
        });

        // A new request arrived 10 minutes ago
        var newRequest = new RegistrationRequest
        {
            GoogleSubject = "sub-2",
            Email = "user2@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-10),
            AdminNotificationSentAt = null
        };
        context.RegistrationRequests.Add(newRequest);
        await SetLastSentAtAsync(context, now.AddMinutes(-65));
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        var message = Assert.Single(fakeSender.SentMessages);
        Assert.Equal(1, message.NewRequestCount);
        Assert.Equal(2, message.TotalPendingRequestCount);

        var updated = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == newRequest.Id);
        Assert.Equal(now, updated.AdminNotificationSentAt);
    }

    [Fact]
    public async Task RequestIsSentExactlyAtCooldownBoundary()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        // A notification was sent exactly 60 minutes ago
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-65),
            AdminNotificationSentAt = now.AddHours(-1)
        });

        // A new request arrived
        var newRequest = new RegistrationRequest
        {
            GoogleSubject = "sub-2",
            Email = "user2@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now.AddMinutes(-5),
            AdminNotificationSentAt = null
        };
        context.RegistrationRequests.Add(newRequest);
        await SetLastSentAtAsync(context, now.AddHours(-1));
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        Assert.Single(fakeSender.SentMessages);

        var updated = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == newRequest.Id);
        Assert.Equal(now, updated.AdminNotificationSentAt);
    }

    [Fact]
    public async Task ApprovedAndRejectedRequestsAreIgnored()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        context.RegistrationRequests.AddRange(
            new RegistrationRequest
            {
                GoogleSubject = "sub-app",
                Email = "app@example.test",
                Status = RegistrationRequestStatus.Approved,
                CreatedAt = now.AddHours(-2),
                DecidedAt = now.AddHours(-1),
                AdminNotificationSentAt = null
            },
            new RegistrationRequest
            {
                GoogleSubject = "sub-rej",
                Email = "rej@example.test",
                Status = RegistrationRequestStatus.Rejected,
                CreatedAt = now.AddHours(-2),
                DecidedAt = now.AddHours(-1),
                AdminNotificationSentAt = null
            });
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.True(result);
        Assert.Empty(fakeSender.SentMessages);
    }

    [Fact]
    public async Task SmtpFailureLeavesAdminNotificationSentAtNull()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        var req = new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        };
        context.RegistrationRequests.Add(req);
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender
        {
            ExceptionToThrow = new InvalidOperationException("SMTP connection failed.")
        };
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        var result = await service.RunNotificationSafelyAsync();

        Assert.False(result);

        var reloaded = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == req.Id);
        Assert.Null(reloaded.AdminNotificationSentAt);
    }

    [Fact]
    public async Task FailedRunRetriesSuccessfullyOnSubsequentRun()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new TestTimeProvider(now);

        var req = new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        };
        context.RegistrationRequests.Add(req);
        await context.SaveChangesAsync();

        var fakeSender = new FakeRegistrationNotificationSender
        {
            ExceptionToThrow = new InvalidOperationException("SMTP transient failure")
        };
        var service = CreateService(context, fakeSender, CreateValidOptions(), timeProvider);

        // Run 1: fails
        var run1 = await service.RunNotificationSafelyAsync();
        Assert.False(run1);
        var intermediate = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == req.Id);
        Assert.Null(intermediate.AdminNotificationSentAt);

        // Run 2: SMTP recovered
        fakeSender.ExceptionToThrow = null;
        timeProvider.SetUtcNow(now.AddMinutes(5));
        var run2 = await service.RunNotificationSafelyAsync();
        Assert.True(run2);
        Assert.Single(fakeSender.SentMessages);

        var reloaded = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == req.Id);
        Assert.Equal(now.AddMinutes(5), reloaded.AdminNotificationSentAt);
    }

    [Fact]
    public async Task SmtpFailureReturnsFalseWithoutThrowing()
    {
        await using var context = await CreateInitializedContextAsync();
        var fakeSender = new FakeRegistrationNotificationSender
        {
            ExceptionToThrow = new Exception("Critical SMTP crash")
        };
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-1",
            Email = "user1@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, fakeSender, CreateValidOptions());

        var exception = await Record.ExceptionAsync(() => service.RunNotificationSafelyAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task CancellationIsHandledGracefully()
    {
        await using var context = await CreateInitializedContextAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var fakeSender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, fakeSender, CreateValidOptions());

        var result = await service.RunNotificationSafelyAsync(cts.Token);

        Assert.False(result);
        Assert.Empty(fakeSender.SentMessages);
    }

    [Fact]
    public void NotificationSenderReceivesOnlyCountsAndNoApplicantData()
    {
        var method = typeof(IRegistrationNotificationSender).GetMethod(nameof(IRegistrationNotificationSender.SendAsync));
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("newRequestCount", parameters[0].Name);
        Assert.Equal(typeof(int), parameters[0].ParameterType);
        Assert.Equal("totalPendingRequestCount", parameters[1].Name);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
    }

    [Fact]
    public void SmtpMessageContainsOnlyAggregateCountsAndAdministrativeInstruction()
    {
        var options = CreateValidOptions();

        var message = SmtpRegistrationNotificationSender.CreateMessage(options, 2, 5);

        Assert.Equal("Neue Registrierungsanträge bei TourEd", message.Subject);
        Assert.Equal("auto-generated", message.Headers["Auto-Submitted"]);
        Assert.Equal(options.SenderAddress, Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal(options.RecipientAddress, Assert.Single(message.To.Mailboxes).Address);
        var body = Assert.IsType<TextPart>(message.Body).Text;
        Assert.Contains("2 neue Registrierungsanträge", body);
        Assert.Contains("5 Registrierungsanträge offen", body);
        Assert.Contains("TourEd.Admin", body);
        Assert.DoesNotContain("@", body);
        Assert.DoesNotContain("Google", body);
        Assert.DoesNotContain("http", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CooldownSurvivesDeletionOfPreviouslyNotifiedRequests()
    {
        await using var context = await CreateInitializedContextAsync();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-deleted",
            Email = "deleted@example.test",
            Status = RegistrationRequestStatus.Approved,
            CreatedAt = now.AddHours(-1),
            AdminNotificationSentAt = now.AddMinutes(-30)
        });
        await context.SaveChangesAsync();
        await SetLastSentAtAsync(context, now.AddMinutes(-30));
        await context.RegistrationRequests.ExecuteDeleteAsync();

        context.RegistrationRequests.Add(new RegistrationRequest
        {
            GoogleSubject = "sub-new-after-delete",
            Email = "new-after-delete@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var sender = new FakeRegistrationNotificationSender();
        var service = CreateService(context, sender, CreateValidOptions(), new TestTimeProvider(now));

        Assert.True(await service.RunNotificationSafelyAsync());
        Assert.Empty(sender.SentMessages);
    }

    #endregion

    #region Registration Logic & Repository Tests

    [Fact]
    public async Task RepeatedLoginForAlreadyNotifiedPendingRequestDoesNotResetSentAt()
    {
        await using var context = await CreateInitializedContextAsync();
        var repository = new TouredRepository(context);
        var sentAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var request = new RegistrationRequest
        {
            GoogleSubject = "sub-repeat",
            Email = "repeat@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = sentAt.AddMinutes(-30),
            AdminNotificationSentAt = sentAt
        };
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();

        var result = await repository.RecordOrUpdateRegistrationRequestAsync("sub-repeat", "repeat@example.test");

        Assert.Equal(sentAt, result.AdminNotificationSentAt);
        var fromDb = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(sentAt, fromDb.AdminNotificationSentAt);
    }

    [Fact]
    public async Task EmailChangeForPendingRequestDoesNotResetSentAt()
    {
        await using var context = await CreateInitializedContextAsync();
        var repository = new TouredRepository(context);
        var sentAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var request = new RegistrationRequest
        {
            GoogleSubject = "sub-change-email",
            Email = "old-email@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = sentAt.AddMinutes(-30),
            AdminNotificationSentAt = sentAt
        };
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();

        var result = await repository.RecordOrUpdateRegistrationRequestAsync("sub-change-email", "new-email@example.test");

        Assert.Equal("new-email@example.test", result.Email);
        Assert.Equal(sentAt, result.AdminNotificationSentAt);
        var fromDb = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(sentAt, fromDb.AdminNotificationSentAt);
    }

    [Fact]
    public async Task RePendingApprovedRequestResetsSentAtToNull()
    {
        await using var context = await CreateInitializedContextAsync();
        var repository = new TouredRepository(context);
        var sentAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var request = new RegistrationRequest
        {
            GoogleSubject = "sub-repending",
            Email = "user@example.test",
            Status = RegistrationRequestStatus.Approved,
            CreatedAt = sentAt.AddDays(-5),
            DecidedAt = sentAt.AddDays(-4),
            AdminNotificationSentAt = sentAt
        };
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();

        var result = await repository.RecordOrUpdateRegistrationRequestAsync("sub-repending", "user@example.test");

        Assert.Equal(RegistrationRequestStatus.Pending, result.Status);
        Assert.Null(result.AdminNotificationSentAt);
        var fromDb = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        Assert.Null(fromDb.AdminNotificationSentAt);
    }

    [Fact]
    public async Task ExistingOpenRequestFromMigrationHasNullSentAtAndCanBeNotified()
    {
        await using var context = await CreateInitializedContextAsync();
        var repository = new TouredRepository(context);

        var request = new RegistrationRequest
        {
            GoogleSubject = "sub-migrated",
            Email = "migrated@example.test",
            Status = RegistrationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            AdminNotificationSentAt = null
        };
        context.RegistrationRequests.Add(request);
        await context.SaveChangesAsync();

        var unnotifiedIds = await repository.GetUnnotifiedPendingRegistrationRequestIdsAsync();
        Assert.Contains(request.Id, unnotifiedIds);
    }

    #endregion

    #region Migration Tests

    [Fact]
    public async Task MigrationExistsAndPreservesExistingData()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(PreviousMigration);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email, DefaultStampingProviderId) VALUES (11, 'existing-user@example.test', 1);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO RegistrationRequests (Id, GoogleSubject, Email, Status, CreatedAt) " +
            "VALUES (21, 'existing-sub', 'existing-req@example.test', 'Pending', '2026-09-01 10:00:00');");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AdminAuditEntries (Id, CreatedAt, ActorUserId, Action, TargetUserId, RegistrationRequestId) " +
            "VALUES (31, '2026-09-01 10:05:00', 11, 'registration.approved', 11, 21);");

        await context.Database.MigrateAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(CurrentMigration, appliedMigrations);

        var request = await context.RegistrationRequests.AsNoTracking().SingleAsync(r => r.Id == 21);
        Assert.Equal("existing-sub", request.GoogleSubject);
        Assert.Equal("existing-req@example.test", request.Email);
        Assert.Equal(RegistrationRequestStatus.Pending, request.Status);
        Assert.Null(request.AdminNotificationSentAt);

        var audit = await context.AdminAuditEntries.AsNoTracking().SingleAsync(a => a.Id == 31);
        Assert.Equal(11, audit.ActorUserId);
        Assert.Equal("registration.approved", audit.Action);

        var user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == 11);
        Assert.Equal("existing-user@example.test", user.Email);

        var notificationState = await context.RegistrationNotificationStates.AsNoTracking().SingleAsync();
        Assert.Equal(RegistrationNotificationState.SingletonId, notificationState.Id);
        Assert.Null(notificationState.LastSentAt);
    }

    #endregion

    #region Helpers & Test Infrastructure

    private RegistrationNotificationOptions CreateValidOptions() => new()
    {
        Enabled = true,
        SmtpHost = "smtp.ionos.de",
        SmtpPort = 587,
        SmtpUsername = "mail.admin@baelgun.de",
        SmtpPassword = "test-secret-password",
        SenderAddress = "toured@baelgun.de",
        RecipientAddress = "admin@baelgun.de"
    };

    private RegistrationRequestNotificationService CreateService(
        DataContext context,
        IRegistrationNotificationSender sender,
        RegistrationNotificationOptions options,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddScoped<TouredRepository>();
        var serviceProvider = services.BuildServiceProvider();

        return new RegistrationRequestNotificationService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            sender,
            Options.Create(options),
            NullLogger<RegistrationRequestNotificationService>.Instance,
            timeProvider ?? TimeProvider.System);
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

    private static async Task SetLastSentAtAsync(DataContext context, DateTime lastSentAt)
    {
        await context.RegistrationNotificationStates
            .Where(state => state.Id == RegistrationNotificationState.SingletonId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.LastSentAt, lastSentAt));
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class FakeRegistrationNotificationSender : IRegistrationNotificationSender
    {
        public List<(int NewRequestCount, int TotalPendingRequestCount)> SentMessages { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task SendAsync(int newRequestCount, int totalPendingRequestCount, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            SentMessages.Add((newRequestCount, totalPendingRequestCount));
            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    #endregion
}
