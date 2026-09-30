namespace Api.Imports;

public interface IHarzerWandernadelImportService
{
    Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
