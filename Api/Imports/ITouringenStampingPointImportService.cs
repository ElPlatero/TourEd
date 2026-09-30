namespace Api.Imports;

public interface ITouringenStampingPointImportService
{
    Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default);
}
