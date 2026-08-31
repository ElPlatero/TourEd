using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface ITouringenStampingPointImportService
{
    Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
