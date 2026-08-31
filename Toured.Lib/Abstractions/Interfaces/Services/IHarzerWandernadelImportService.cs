using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IHarzerWandernadelImportService
{
    Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
