using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Services;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IHtmlParsingService
{
    Task<string?> GetRawDmoStringAsync(Uri uri);
}
