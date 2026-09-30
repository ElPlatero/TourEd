using Api.Entities;

namespace Api.Repositories;

public interface IRegistrationRequestService
{
    Task<RegistrationRequest> RecordOrUpdateRegistrationRequestAsync(string googleSubject, string email, CancellationToken cancellationToken = default);
    Task MarkRegistrationRequestApprovedAsync(string googleSubject, CancellationToken cancellationToken = default);
}
