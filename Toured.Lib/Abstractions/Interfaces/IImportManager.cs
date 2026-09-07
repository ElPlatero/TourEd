namespace TourEd.Lib.Abstractions.Interfaces;

public interface IImportManager
{
    Task ImportTouringenDataAsync(CancellationToken cancellationToken = default);
    Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default);
    Task<TourEd.Lib.Abstractions.Models.UserDataImportResult> ImportUserDataAsync(Stream stream);
}
