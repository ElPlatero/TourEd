using Microsoft.EntityFrameworkCore.Storage;
using TourEd.Lib.Abstractions;

namespace Api.Repositories;

public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly DataContext _dbContext;

    public UnitOfWorkFactory(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return JoinedUnitOfWork.Instance;
        }

        return new UnitOfWork(await _dbContext.Database.BeginTransactionAsync(cancellationToken));
    }

    /// <summary>Owns a transaction; disposing an uncommitted EF Core transaction rolls it back.</summary>
    private sealed class UnitOfWork : IUnitOfWork
    {
        private readonly IDbContextTransaction _transaction;

        public UnitOfWork(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }

    /// <summary>Participates in the already active transaction, which its owner commits or rolls back.</summary>
    private sealed class JoinedUnitOfWork : IUnitOfWork
    {
        public static readonly JoinedUnitOfWork Instance = new();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
