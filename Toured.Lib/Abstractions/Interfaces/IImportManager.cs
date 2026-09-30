namespace TourEd.Lib.Abstractions.Interfaces;

public interface IImportManager
{
    Task ImportTouringenDataAsync(CancellationToken cancellationToken = default);
    Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default);
    Task<TourEd.Lib.Abstractions.Models.UserDataImportResult> ImportUserDataAsync(
        TourEd.Lib.Abstractions.Models.User user,
        Stream stream,
        CancellationToken cancellationToken = default);
}
