using Api.Entities;

namespace Api.Imports;

public interface IImportService<out T>
{
    IEnumerable<T> Import(RawArea[]? inputData);
}
