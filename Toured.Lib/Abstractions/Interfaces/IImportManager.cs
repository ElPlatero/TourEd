namespace TourEd.Lib.Abstractions.Interfaces;

public interface IImportManager
{
    Task ImportTouringenDataAsync();
    Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default);
    Task ImportUserDataAsync(Stream stream);
}
