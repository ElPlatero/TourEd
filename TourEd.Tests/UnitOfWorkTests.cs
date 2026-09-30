using Api.Entities;
using Api.Managers;
using Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TourEd.Tests;

public sealed class UnitOfWorkTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"toured-uow-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task CommitPersistsChanges()
    {
        await using (var context = await CreateContextAsync())
        {
            await using var unitOfWork = await new UnitOfWorkFactory(context).BeginAsync();
            context.Users.Add(new User { Email = "commit@example.test" });
            await context.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }

        Assert.True(await UserExistsAsync("commit@example.test"));
    }

    [Fact]
    public async Task DisposingWithoutCommitRollsBack()
    {
        await using (var context = await CreateContextAsync())
        {
            await using var unitOfWork = await new UnitOfWorkFactory(context).BeginAsync();
            context.Users.Add(new User { Email = "rollback@example.test" });
            await context.SaveChangesAsync();
        }

        Assert.False(await UserExistsAsync("rollback@example.test"));
    }

    [Fact]
    public async Task NestedUnitJoinsOuterTransaction()
    {
        await using (var context = await CreateContextAsync())
        {
            var factory = new UnitOfWorkFactory(context);
            await using var outer = await factory.BeginAsync();
            await using (var inner = await factory.BeginAsync())
            {
                context.Users.Add(new User { Email = "nested@example.test" });
                await context.SaveChangesAsync();
                await inner.CommitAsync();
            }

            Assert.NotNull(context.Database.CurrentTransaction);
        }

        Assert.False(await UserExistsAsync("nested@example.test"));
    }

    [Fact]
    public async Task ManagerTransactionsJoinAnOuterUnitOfWork()
    {
        int userId;
        int actorId;
        await using (var context = await CreateContextAsync())
        {
            var user = new User { Email = "target@example.test" };
            var actor = new User { Email = "actor@example.test" };
            context.Users.AddRange(user, actor);
            await context.SaveChangesAsync();
            (userId, actorId) = (user.Id, actor.Id);
        }

        await using (var context = await CreateContextAsync())
        {
            await using var outer = await new UnitOfWorkFactory(context).BeginAsync();
            var manager = new AdminUserManager(
                new UserRepository(context),
                new RegistrationRequestRepository(context),
                new AdminAuditRepository(context),
                new StampingProviderRepository(context),
                new UnitOfWorkFactory(context));
            Assert.True(await manager.DeleteUserAsync(userId, actorId, CancellationToken.None));
        }

        Assert.True(await UserExistsAsync("target@example.test"));
    }

    [Fact]
    public async Task FinishedUnitReleasesTheTransaction()
    {
        await using var context = await CreateContextAsync();
        var factory = new UnitOfWorkFactory(context);

        await using (var unitOfWork = await factory.BeginAsync())
        {
            await unitOfWork.CommitAsync();
        }

        Assert.Null(context.Database.CurrentTransaction);
        await using var next = await factory.BeginAsync();
        Assert.NotNull(context.Database.CurrentTransaction);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        await using var context = await CreateContextAsync();
        return await context.Users.AnyAsync(user => user.Email == email);
    }

    private async Task<DataContext> CreateContextAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TouredDb"] = $"Data Source={_databasePath};Pooling=False"
            })
            .Build();
        var context = new DataContext(TestDbContextOptions.For(configuration));
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
