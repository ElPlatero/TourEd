using Api.Dto;
using Api.Repositories;

namespace Api.Managers;

public sealed class AdminUserManager
{
    private readonly TouredRepository _repository;

    public AdminUserManager(TouredRepository repository)
    {
        _repository = repository;
    }

    public Task<List<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken)
        => _repository.GetAdminUsersAsync(cancellationToken);

    public Task<List<AdminProviderDto>> GetProvidersAsync(CancellationToken cancellationToken)
        => _repository.GetAdminProvidersAsync(cancellationToken);

    public Task<List<AdminAuditEntryDto>> GetAuditEntriesAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
        => _repository.GetAdminAuditEntriesAsync(offset, limit, cancellationToken);

    public Task<bool> DeleteUserAsync(int userId, int actorUserId, CancellationToken cancellationToken)
        => _repository.DeleteAdminUserAsync(userId, actorUserId, cancellationToken);

    public Task<AdminUserDto?> UpdateProvidersAsync(
        int userId,
        UpdateAdminUserProvidersRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken)
        => _repository.UpdateAdminUserProvidersAsync(userId, request, actorUserId, cancellationToken);

    public Task<List<AdminRegistrationRequestDto>> GetRegistrationRequestsAsync(
        string? status,
        CancellationToken cancellationToken)
        => _repository.GetRegistrationRequestsAsync(status, cancellationToken);

    public Task<AdminRegistrationRequestDto?> ApproveRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken)
        => _repository.ApproveRegistrationRequestAsync(id, actorUserId, cancellationToken);

    public Task<AdminRegistrationRequestDto?> RejectRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken)
        => _repository.RejectRegistrationRequestAsync(id, actorUserId, cancellationToken);
}
