namespace Api.Repositories;

/// <summary>A database transaction. Disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
