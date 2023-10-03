namespace TourEd.Lib.Abstractions;

public interface IUnitOfWork : IDisposable
{
    void Commit();
    Task CommitAsync();
}
