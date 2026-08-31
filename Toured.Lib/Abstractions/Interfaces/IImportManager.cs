namespace TourEd.Lib.Abstractions.Interfaces;

public interface IImportManager
{
    Task ImportTouringenDataAsync(CancellationToken cancellationToken = default);
    Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default);
    Task ImportUserDataAsync(Stream stream);
}
