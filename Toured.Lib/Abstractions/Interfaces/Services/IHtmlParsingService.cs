namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IHtmlParsingService
{
    Task<string?> GetRawDmoStringAsync(Uri uri, CancellationToken cancellationToken = default);
}
