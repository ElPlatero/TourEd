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

    public Task<AdminUserDto?> UpdateProvidersAsync(
        int userId,
        UpdateAdminUserProvidersRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken)
        => _repository.UpdateAdminUserProvidersAsync(userId, request, actorUserId, cancellationToken);
}
