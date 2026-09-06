using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class DataContextLoggingTests
{
    [Fact]
    public async Task DatabaseCommandsKeepPersonalParametersOutOfLogsIncludingOnFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = "Data Source=:memory:"
            }).Build();
        var messages = new List<string>();
        await using var context = new LoggingDataContext(configuration, messages);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        messages.Clear();

        var email = $"logging-{Guid.NewGuid():N}@example.test";
        var subject = $"subject-{Guid.NewGuid():N}";
        context.Users.Add(new User { Email = email, GoogleSubject = subject });
        await context.SaveChangesAsync();
        Assert.True(await context.Users.AsNoTracking().AnyAsync(user => user.Email == email));

        context.Users.Add(new User { Email = email, GoogleSubject = subject });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var log = string.Join(Environment.NewLine, messages);
        Assert.Contains("INSERT INTO", log, StringComparison.Ordinal);
        Assert.Contains("SELECT", log, StringComparison.Ordinal);
        Assert.Contains("Failed executing DbCommand", log, StringComparison.Ordinal);
        Assert.DoesNotContain(email, log, StringComparison.Ordinal);
        Assert.DoesNotContain(subject, log, StringComparison.Ordinal);
    }

    private sealed class LoggingDataContext(IConfiguration configuration, List<string> messages)
        : DataContext(configuration)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            base.OnConfiguring(options);
            options.LogTo(messages.Add, LogLevel.Information);
        }
    }
}
