using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IImportService<out T>
{
    IEnumerable<T> Import(RawArea[]? inputData);
}
