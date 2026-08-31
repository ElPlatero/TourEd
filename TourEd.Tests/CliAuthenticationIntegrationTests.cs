using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api.Authentication;
using Api.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Tests;

public sealed class CliAuthenticationIntegrationTests : IAsyncLifetime
{
    private const string RemovedUserHeader = "toured-user";
    private const string CliToken = "test-only-256-bit-cli-token-0123456789abcdef0123456789abcdef";
    private const string UserEmail = "cli-user@example.test";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-cli-tests-{Guid.NewGuid():N}.db");
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"toured-cli-keys-{Guid.NewGuid():N}");
    private CliWebApplicationFactory _factory = null!;
    private int _userId;

    public async Task InitializeAsync()
    {
        _factory = new CliWebApplicationFactory(_databasePath, _keysPath, UserEmail);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.MigrateAsync();
        var user = new User { Email = UserEmail };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        _userId = user.Id;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong-token")]
    [InlineData("Bearer ")]
    public async Task MissingWrongAndEmptyTokensReturnUnauthorized(string? authorization)
    {
        using var client = CreateClient(_factory);
        if (authorization is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        }

        var response = await client.PostAsync("/api/admin/imports/touringen", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task ValidTokenCreatesConfiguredUserClaimsAndAllowsAllImports()
    {
        using var client = CreateClient(_factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CliToken);

        var touringenResponse = await client.PostAsync("/api/admin/imports/touringen", content: null);
        var harzerWandernadelResponse = await client.PostAsync("/api/admin/imports/harzer-wandernadel", content: null);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("1;;")), "csvImport", "visits.csv");
        var userResponse = await client.PostAsync("/api/admin/imports", form);

        Assert.Equal(HttpStatusCode.OK, touringenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, harzerWandernadelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        var importManager = _factory.Services.GetRequiredService<CapturingImportManager>();
        Assert.Equal(1, importManager.TouringenImportCount);
        Assert.Equal(1, importManager.HarzerWandernadelImportCount);
        Assert.Equal(1, importManager.UserImportCount);
        Assert.Equal(TouredAuthenticationSchemes.CliBearer, importManager.AuthenticationType);
        Assert.Collection(
            importManager.Claims.OrderBy(claim => claim.Type),
            claim =>
            {
                Assert.Equal(Constants.ClaimsNames.UserEmail, claim.Type);
                Assert.Equal(UserEmail, claim.Value);
            },
            claim =>
            {
                Assert.Equal(Constants.ClaimsNames.UserId, claim.Type);
                Assert.Equal(_userId.ToString(), claim.Value);
            });
    }

    [Fact]
    public async Task ConfiguredUnknownUserIsRejected()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"toured-cli-missing-user-{Guid.NewGuid():N}.db");
        var keysPath = Path.Combine(Path.GetTempPath(), $"toured-cli-missing-user-keys-{Guid.NewGuid():N}");
        await using var factory = new CliWebApplicationFactory(databasePath, keysPath, "missing@example.test");
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<DataContext>().Database.MigrateAsync();
            }

            using var client = CreateClient(factory);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CliToken);

            var response = await client.PostAsync("/api/admin/imports/touringen", content: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, factory.Services.GetRequiredService<CapturingImportManager>().TouringenImportCount);
        }
        finally
        {
            DeleteFile(databasePath);
            DeleteDirectory(keysPath);
        }
    }

    [Fact]
    public async Task CookieAndRemovedUserHeaderAloneCannotAuthorizeImports()
    {
        using var cookieClient = CreateClient(_factory);
        cookieClient.DefaultRequestHeaders.Add("Cookie", CreateSessionCookie(_factory.Services, _userId, UserEmail));
        var cookieResponse = await cookieClient.PostAsync("/api/admin/imports/touringen", content: null);

        using var legacyClient = CreateClient(_factory);
        legacyClient.DefaultRequestHeaders.Add(RemovedUserHeader, UserEmail);
        var legacyResponse = await legacyClient.PostAsync("/api/admin/imports/touringen", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, cookieResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legacyResponse.StatusCode);
        Assert.Equal(0, _factory.Services.GetRequiredService<CapturingImportManager>().TouringenImportCount);
    }

    [Fact]
    public async Task CliBearerDoesNotAuthorizeBrowserEndpoints()
    {
        using var client = CreateClient(_factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CliToken);

        var filteredPoints = await client.GetAsync("/api/points?vis=true");
        var visit = await client.GetAsync("/api/points/1");

        Assert.Equal(HttpStatusCode.Unauthorized, filteredPoints.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, visit.StatusCode);
    }

    [Fact]
    public async Task SuppliedTokenDoesNotAppearInResponseOrLogs()
    {
        const string sensitiveWrongToken = "must-never-appear-in-errors-or-logs";
        _factory.Logs.Clear();
        using var client = CreateClient(_factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sensitiveWrongToken);

        var response = await client.PostAsync("/api/admin/imports/touringen", content: null);
        var body = await response.Content.ReadAsStringAsync();
        var logs = string.Join(Environment.NewLine, _factory.Logs.Messages);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(sensitiveWrongToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveWrongToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain($"Bearer {sensitiveWrongToken}", logs, StringComparison.Ordinal);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        DeleteFile(_databasePath);
        DeleteDirectory(_keysPath);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

    private static string CreateSessionCookie(IServiceProvider services, int userId, string userEmail)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(TouredAuthenticationSchemes.Cookie);
        Claim[] claims =
        [
            new(Constants.ClaimsNames.UserId, userId.ToString()),
            new(Constants.ClaimsNames.UserEmail, userEmail)
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TouredAuthenticationSchemes.Cookie));
        var ticket = new AuthenticationTicket(principal, TouredAuthenticationSchemes.Cookie);
        var protectedTicket = options.TicketDataFormat.Protect(ticket);
        return $"{options.Cookie.Name}={protectedTicket}";
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

    private sealed class CliWebApplicationFactory(
        string databasePath,
        string keysPath,
        string cliUserEmail) : WebApplicationFactory<Program>
    {
        public CapturingLoggerProvider Logs { get; } = new();

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
                    ["Authentication:Cli:UserEmail"] = cliUserEmail,
                    ["DataProtection:KeysPath"] = keysPath
                }));
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IImportManager>();
                services.AddSingleton<CapturingImportManager>();
                services.AddSingleton<IImportManager>(provider => provider.GetRequiredService<CapturingImportManager>());
            });
        }
    }

    private sealed class CapturingImportManager(IHttpContextAccessor httpContextAccessor) : IImportManager
    {
        private int _touringenImportCount;
        private int _harzerWandernadelImportCount;
        private int _userImportCount;

        public int TouringenImportCount => _touringenImportCount;
        public int HarzerWandernadelImportCount => _harzerWandernadelImportCount;
        public int UserImportCount => _userImportCount;
        public string? AuthenticationType { get; private set; }
        public Claim[] Claims { get; private set; } = [];

        public Task ImportTouringenDataAsync(CancellationToken cancellationToken = default)
        {
            CapturePrincipal();
            Interlocked.Increment(ref _touringenImportCount);
            return Task.CompletedTask;
        }

        public Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default)
        {
            CapturePrincipal();
            Interlocked.Increment(ref _harzerWandernadelImportCount);
            return Task.CompletedTask;
        }

        public Task ImportUserDataAsync(Stream stream)
        {
            CapturePrincipal();
            Interlocked.Increment(ref _userImportCount);
            return Task.CompletedTask;
        }

        private void CapturePrincipal()
        {
            var principal = httpContextAccessor.HttpContext?.User;
            AuthenticationType = principal?.Identity?.AuthenticationType;
            Claims = principal?.Claims.Select(claim => new Claim(claim.Type, claim.Value)).ToArray() ?? [];
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Clear()
        {
            while (_messages.TryDequeue(out _))
            {
            }
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
                if (exception is not null)
                {
                    messages.Enqueue(exception.ToString());
                }
            }
        }
    }
}
