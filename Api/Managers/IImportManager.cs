using Api.Dto;
using Api.Entities;

namespace Api.Managers;

public interface IImportManager
{
    Task ImportTouringenDataAsync(CancellationToken cancellationToken = default);
    Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default);
    Task<UserDataImportResult> ImportUserDataAsync(
        User user,
        Stream stream,
        CancellationToken cancellationToken = default);
}
