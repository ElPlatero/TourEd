using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IRegistrationRequestService
{
    Task<RegistrationRequest> RecordOrUpdateRegistrationRequestAsync(string googleSubject, string email, CancellationToken cancellationToken = default);
    Task MarkRegistrationRequestApprovedAsync(string googleSubject, CancellationToken cancellationToken = default);
}
