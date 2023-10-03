namespace TourEd.Lib.Abstractions.Interfaces;

public interface IImportManager
{
    Task ImportTouringenDataAsync();
    Task ImportUserDataAsync(Stream stream);
}
