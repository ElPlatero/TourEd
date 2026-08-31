using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface ITouringenStampingPointImportService
{
    Task<TouringenStampingPointSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
