namespace TourEd.Lib.Abstractions;

/// <summary>
/// Starts units of work. A unit started while another one is active joins it:
/// only the outermost unit commits or rolls back.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}
