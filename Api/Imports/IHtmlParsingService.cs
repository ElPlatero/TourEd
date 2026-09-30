namespace Api.Imports;

public interface IHtmlParsingService
{
    Task<string?> GetRawDmoStringAsync(Uri uri, CancellationToken cancellationToken = default);
}
