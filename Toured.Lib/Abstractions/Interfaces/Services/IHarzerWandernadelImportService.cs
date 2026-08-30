using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IHarzerWandernadelImportService
{
    Task<IReadOnlyList<StampingPoint>> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
