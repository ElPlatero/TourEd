namespace TourEd.Lib.Abstractions;

public interface IUnitOfWork : IDisposable
{
    Task CommitAsync();
}
