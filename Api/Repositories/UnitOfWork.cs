using Microsoft.EntityFrameworkCore.Storage;
using TourEd.Lib.Abstractions;

namespace Api.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private bool _disposed;
    private readonly IDbContextTransaction _transaction;
    private bool _committed;

    public UnitOfWork(DataContext dbContext)
    {
        _transaction = dbContext.Database.BeginTransaction();
    }

    public void Commit()
    {
        _committed = true;
        _transaction.Commit();
    }

    public async Task CommitAsync()
    {
        _committed = true;
        await _transaction.CommitAsync();
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (!_committed)
                {
                    _transaction.Rollback();
                }
                _transaction.Dispose();
            }
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
    }
}
